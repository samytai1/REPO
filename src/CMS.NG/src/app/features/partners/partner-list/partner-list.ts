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
import { TooltipModule } from 'primeng/tooltip';

import { EMPTY_PARTNER_QUERY, Partner, PartnerQuery } from '@core/models';
import { ListStateService, PartnerService } from '@core/services';

/** Persisted list-page state keys — `{table}-list-*` per the code-gen convention. */
const FILTERS_KEY = 'partner-list-filters';
const SORT_KEY = 'partner-list-sort';
const PAGE_KEY = 'partner-list-page';

interface SortState {
  field: string;
  order: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** 合作夥伴 Partner — list page with a filter drawer, sorting and paging. */
@Component({
  selector: 'app-partner-list',
  imports: [
    FormsModule,
    ButtonModule,
    DrawerModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './partner-list.html',
  styleUrl: './partner-list.scss',
})
export class PartnerList implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly listState = inject(ListStateService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  /** The filter currently applied to the table. */
  protected filters: PartnerQuery = { ...EMPTY_PARTNER_QUERY };

  /** The draft edited inside the drawer; only copied into `filters` on 套用. */
  protected draftFilters: PartnerQuery = { ...EMPTY_PARTNER_QUERY };

  protected sort: SortState = { field: 'displayOrder', order: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  /** Tri-state options for the derived 圖片 filter — null leaves it unfiltered. */
  protected readonly imageOptions: { label: string; value: boolean | null }[] = [
    { label: '不限', value: null },
    { label: '有', value: true },
    { label: '無', value: false },
  ];

  ngOnInit(): void {
    this.filters = this.listState.read<PartnerQuery>(FILTERS_KEY) ?? { ...EMPTY_PARTNER_QUERY };
    this.sort = this.listState.read<SortState>(SORT_KEY) ?? this.sort;
    this.page = this.listState.read<PageState>(PAGE_KEY) ?? this.page;
    this.draftFilters = { ...this.filters };

    this.load();
  }

  /**
   * True when at least one filter is set — drives the "filtered" badge on the drawer button.
   * `hasImage` is compared against null, not truthiness: `false` is still a filter.
   */
  protected get hasActiveFilters(): boolean {
    return (
      !!this.filters.keyword ||
      this.filters.displayOrderFrom !== null ||
      this.filters.displayOrderTo !== null ||
      this.filters.hasImage !== null
    );
  }

  protected load(): void {
    this.loading.set(true);

    this.service.query(this.filters).subscribe({
      next: (partners) => {
        this.partners.set(partners);
        this.loading.set(false);
      },
      error: () => {
        this.partners.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得合作夥伴資料。',
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
    this.draftFilters = { ...EMPTY_PARTNER_QUERY };
    this.filters = { ...EMPTY_PARTNER_QUERY };
    this.page = { ...this.page, first: 0 };

    this.listState.clear(FILTERS_KEY);
    this.listState.write(PAGE_KEY, this.page);

    this.load();
  }

  /** p-table emits both sort and page changes here; both are persisted. */
  protected onStateChange(event: TableLazyLoadEvent): void {
    if (event.sortField) {
      this.sort = {
        field: Array.isArray(event.sortField) ? event.sortField[0] : event.sortField,
        order: event.sortOrder ?? 1,
      };
      this.listState.write(SORT_KEY, this.sort);
    }

    this.page = { first: event.first ?? 0, rows: event.rows ?? this.page.rows };
    this.listState.write(PAGE_KEY, this.page);
  }

  protected view(partner: Partner): void {
    void this.router.navigate(['/partners', partner.pkid]);
  }

  protected edit(partner: Partner): void {
    void this.router.navigate(['/partners', partner.pkid, 'edit']);
  }

  protected add(): void {
    void this.router.navigate(['/partners', 'new']);
  }

  protected confirmDelete(partner: Partner): void {
    this.confirmationService.confirm({
      header: '刪除合作夥伴',
      message: `確定要刪除主代碼 ${partner.pkid}「${partner.name}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(partner),
    });
  }

  private delete(partner: Partner): void {
    this.service.delete(partner.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: `合作夥伴「${partner.name}」已刪除。`,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error?.status === 409
              ? '此合作夥伴已被認證、課程或課程群組使用，無法刪除。'
              : '請稍後再試一次。',
        }),
    });
  }
}
