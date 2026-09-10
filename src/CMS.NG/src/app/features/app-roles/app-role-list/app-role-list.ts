import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import { AppRole, AppRoleQuery, EMPTY_APP_ROLE_QUERY } from '@core/models';
import { AppRoleService, ListStateService } from '@core/services';

/** Persisted list-page state keys — `{table}-list-*` per the code-gen convention. */
const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

interface SortState {
  field: string;
  order: number;
}

interface PageState {
  first: number;
  rows: number;
}

/**
 * What `(onSort)` actually emits for a single-column sort — PrimeNG's `sortMeta`, which has no
 * TypeScript export. `(onPage)` emits `{ first, rows }`, a subset of `TableLazyLoadEvent`.
 */
interface TableSortEvent {
  field?: string;
  order?: number;
  sortField?: undefined;
  sortOrder?: undefined;
  first?: undefined;
  rows?: undefined;
}

/** Every shape the shared sort/page handler receives; each name is declared on both so `??` can read either. */
type TableStateEvent =
  | (TableLazyLoadEvent & { field?: undefined; order?: undefined })
  | TableSortEvent;

/** 角色 AppRole — list page with a filter drawer, sorting and paging. */
@Component({
  selector: 'app-app-role-list',
  imports: [
    FormsModule,
    ButtonModule,
    DrawerModule,
    InputNumberModule,
    InputTextModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss',
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly listState = inject(ListStateService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** The filter currently applied to the table. */
  protected filters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };

  /** The draft edited inside the drawer; only copied into `filters` on 套用. */
  protected draftFilters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };

  protected sort: SortState = { field: 'roleId', order: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  ngOnInit(): void {
    this.filters = this.listState.read<AppRoleQuery>(FILTERS_KEY) ?? { ...EMPTY_APP_ROLE_QUERY };
    this.sort = this.listState.read<SortState>(SORT_KEY) ?? this.sort;
    this.page = this.listState.read<PageState>(PAGE_KEY) ?? this.page;
    this.draftFilters = { ...this.filters };

    this.load();
  }

  /** True when at least one filter is set — drives the "filtered" badge on the drawer button. */
  protected get hasActiveFilters(): boolean {
    return (
      !!this.filters.keyword ||
      this.filters.permissionLevelFrom !== null ||
      this.filters.permissionLevelTo !== null
    );
  }

  protected load(): void {
    this.loading.set(true);

    this.service.query(this.filters).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.roles.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得角色資料。',
        });
      },
    });
  }

  protected openDrawer(): void {
    this.draftFilters = { ...this.filters };
    this.drawerVisible.set(true);
  }

  protected applyFilters(): void {
    this.filters = { ...this.draftFilters };
    this.page = { ...this.page, first: 0 };

    this.listState.write(FILTERS_KEY, this.filters);
    this.listState.write(PAGE_KEY, this.page);

    this.drawerVisible.set(false);
    this.load();
  }

  protected resetFilters(): void {
    this.draftFilters = { ...EMPTY_APP_ROLE_QUERY };
    this.filters = { ...EMPTY_APP_ROLE_QUERY };
    this.page = { ...this.page, first: 0 };

    this.listState.clear(FILTERS_KEY);
    this.listState.write(PAGE_KEY, this.page);

    this.load();
  }

  /**
   * p-table emits both sort and page changes here; both are persisted.
   *
   * The two events have different shapes, and neither is the `TableLazyLoadEvent` that only
   * `(onLazyLoad)` sends: `(onSort)` carries `{ field, order }` and `(onPage)` carries
   * `{ first, rows }`. Reading `sortField` alone never saw a sort, and reading a missing `first`
   * as 0 wiped the page on every sort — so both are read by whichever name the event uses.
   */
  protected onStateChange(event: TableStateEvent): void {
    const sortField = event.sortField ?? event.field;
    const sortOrder = event.sortOrder ?? event.order;

    if (sortField) {
      const next: SortState = {
        field: Array.isArray(sortField) ? sortField[0] : sortField,
        order: sortOrder ?? 1,
      };

      // p-table re-emits (onSort) with its current sort every time the value is assigned — on
      // the initial load and after every query — so only a sort that actually changed is a user
      // action. Treating the re-emit as one wiped a restored page back to 0 on every load.
      if (next.field !== this.sort.field || next.order !== this.sort.order) {
        this.sort = next;
        this.listState.write(SORT_KEY, this.sort);

        // A bare sort event carries no page: a new sort order starts from page 1, exactly as the
        // table's own resetPageOnSort does.
        if (event.first === undefined) {
          this.page = { ...this.page, first: 0 };
        }
      }
    }

    if (event.first !== undefined || event.rows !== undefined) {
      this.page = { first: event.first ?? 0, rows: event.rows ?? this.page.rows };
    }

    this.listState.write(PAGE_KEY, this.page);
  }

  protected view(role: AppRole): void {
    void this.router.navigate(['/app-roles', role.roleId]);
  }

  protected edit(role: AppRole): void {
    void this.router.navigate(['/app-roles', role.roleId, 'edit']);
  }

  protected add(): void {
    void this.router.navigate(['/app-roles', 'new']);
  }

  protected confirmDelete(role: AppRole): void {
    this.confirmationService.confirm({
      header: '刪除角色',
      message: `確定要刪除角色「${role.roleName}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(role),
    });
  }

  private delete(role: AppRole): void {
    this.service.delete(role.roleId).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: `角色「${role.roleName}」已刪除。`,
        });
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: '此角色可能仍有使用者或關聯資料。',
        }),
    });
  }
}
