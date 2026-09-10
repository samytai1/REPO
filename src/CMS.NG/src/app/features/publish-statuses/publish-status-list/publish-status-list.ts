import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { EMPTY_PUBLISH_STATUS_QUERY, PublishStatus, PublishStatusQuery } from '@core/models';
import { ListStateService, PublishStatusService } from '@core/services';

/** Persisted list-page state keys — `{table}-list-*` per the code-gen convention. */
const FILTERS_KEY = 'publish-status-list-filters';
const SORT_KEY = 'publish-status-list-sort';
const PAGE_KEY = 'publish-status-list-page';

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

/** 發布狀態 PublishStatus — list page with a filter drawer, sorting and paging. */
@Component({
  selector: 'app-publish-status-list',
  imports: [
    FormsModule,
    ButtonModule,
    DrawerModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
  ],
  templateUrl: './publish-status-list.html',
  styleUrl: './publish-status-list.scss',
})
export class PublishStatusList implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly listState = inject(ListStateService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** The filter currently applied to the table. */
  protected filters: PublishStatusQuery = { ...EMPTY_PUBLISH_STATUS_QUERY };

  /** The draft edited inside the drawer; only copied into `filters` on 套用. */
  protected draftFilters: PublishStatusQuery = { ...EMPTY_PUBLISH_STATUS_QUERY };

  protected sort: SortState = { field: 'pkid', order: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  /** Tri-state options for the three bit columns — null leaves the flag unfiltered. */
  protected readonly boolOptions: { label: string; value: boolean | null }[] = [
    { label: '不限', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  ngOnInit(): void {
    this.filters =
      this.listState.read<PublishStatusQuery>(FILTERS_KEY) ?? { ...EMPTY_PUBLISH_STATUS_QUERY };
    this.sort = this.listState.read<SortState>(SORT_KEY) ?? this.sort;
    this.page = this.listState.read<PageState>(PAGE_KEY) ?? this.page;
    this.draftFilters = { ...this.filters };

    this.load();
  }

  /** True when at least one filter is set — drives the "filtered" badge on the drawer button. */
  protected get hasActiveFilters(): boolean {
    return (
      !!this.filters.keyword ||
      this.filters.pkidFrom !== null ||
      this.filters.pkidTo !== null ||
      this.filters.isDraft !== null ||
      this.filters.isPublished !== null ||
      this.filters.isDiscontinued !== null
    );
  }

  protected load(): void {
    this.loading.set(true);

    this.service.query(this.filters).subscribe({
      next: (statuses) => {
        this.statuses.set(statuses);
        this.loading.set(false);
      },
      error: () => {
        this.statuses.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得發布狀態資料。',
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
    this.draftFilters = { ...EMPTY_PUBLISH_STATUS_QUERY };
    this.filters = { ...EMPTY_PUBLISH_STATUS_QUERY };
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
      this.sort = {
        field: Array.isArray(sortField) ? sortField[0] : sortField,
        order: sortOrder ?? 1,
      };
      this.listState.write(SORT_KEY, this.sort);
    }

    if (event.first !== undefined || event.rows !== undefined) {
      this.page = { first: event.first ?? 0, rows: event.rows ?? this.page.rows };
    } else if (sortField) {
      // A bare sort event carries no page: the table starts a new sort order from page 1.
      this.page = { ...this.page, first: 0 };
    }

    this.listState.write(PAGE_KEY, this.page);
  }

  protected view(status: PublishStatus): void {
    void this.router.navigate(['/publish-statuses', status.pkid]);
  }

  protected edit(status: PublishStatus): void {
    void this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
  }

  protected add(): void {
    void this.router.navigate(['/publish-statuses', 'new']);
  }

  protected confirmDelete(status: PublishStatus): void {
    this.confirmationService.confirm({
      header: '刪除發布狀態',
      message: `確定要刪除主代碼 ${status.pkid}「${status.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(status),
    });
  }

  private delete(status: PublishStatus): void {
    this.service.delete(status.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: `發布狀態「${status.description}」已刪除。`,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error?.status === 409
              ? '此發布狀態已被課程或促銷使用，無法刪除。'
              : '請稍後再試一次。',
        }),
    });
  }
}
