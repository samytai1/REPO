import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin } from 'rxjs';

import {
  Course,
  CourseGroupLookup,
  CourseQuery,
  EMPTY_COURSE_QUERY,
  Partner,
  PublishStatus,
} from '@core/models';
import { CourseService, ListStateService, LookupService } from '@core/services';
import { fromIso, toIso } from '@core/utils/date.util';

/** Persisted list-page state keys — `{table}-list-*` per the code-gen convention. */
const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

interface SortState {
  field: string;
  order: number;
}

interface PageState {
  first: number;
  rows: number;
}

/**
 * The drawer's working copy of the filters. The four date bounds are `Date`s here so
 * `p-datepicker` can bind them; they serialise to ISO strings on 套用.
 */
interface DraftFilters {
  keyword: string | null;
  partnerPkid: number | null;
  courseGroupPkid: number | null;
  publishStatusPkid: number | null;
  canRepeat: boolean | null;
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
}

interface SelectOption<T> {
  label: string;
  value: T;
}

/** 課程 Course — list page with a filter drawer, sorting and paging. */
@Component({
  selector: 'app-course-list',
  imports: [
    DecimalPipe,
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
  ],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss',
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly listState = inject(ListStateService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatus[]>([]);

  /** The filter currently applied to the table. */
  protected filters: CourseQuery = { ...EMPTY_COURSE_QUERY };

  /** The draft edited inside the drawer; only copied into `filters` on 套用. */
  protected draftFilters: DraftFilters = toDraft(EMPTY_COURSE_QUERY);

  protected sort: SortState = { field: 'displayOrder', order: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  protected readonly rowsPerPageOptions = [10, 20, 50, 100];

  /** Tri-state options for the 允許重聽 filter — null leaves it unfiltered. */
  protected readonly repeatOptions: SelectOption<boolean | null>[] = [
    { label: '不限', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  ngOnInit(): void {
    // Merge over the empty query so a filter added after the state was saved still reads as null.
    this.filters = {
      ...EMPTY_COURSE_QUERY,
      ...(this.listState.read<CourseQuery>(FILTERS_KEY) ?? {}),
    };
    this.sort = this.listState.read<SortState>(SORT_KEY) ?? this.sort;
    this.page = this.listState.read<PageState>(PAGE_KEY) ?? this.page;
    this.draftFilters = toDraft(this.filters);

    this.loadLookups();
    this.load();
  }

  /** FK option lists for the drawer, each led by a 不限 entry that clears the filter. */
  protected get partnerOptions(): SelectOption<number | null>[] {
    return [
      { label: '不限', value: null },
      ...this.partners().map((p) => ({ label: p.name, value: p.pkid })),
    ];
  }

  protected get courseGroupOptions(): SelectOption<number | null>[] {
    return [
      { label: '不限', value: null },
      ...this.courseGroups().map((g) => ({ label: g.description, value: g.pkid })),
    ];
  }

  protected get publishStatusOptions(): SelectOption<number | null>[] {
    return [
      { label: '不限', value: null },
      ...this.publishStatuses().map((s) => ({ label: s.description, value: s.pkid })),
    ];
  }

  /**
   * True when at least one filter is set — drives the "filtered" badge on the drawer button.
   * Every non-keyword filter is compared against null, not truthiness: `false` and `0` count.
   */
  protected get hasActiveFilters(): boolean {
    const f = this.filters;
    return (
      !!f.keyword ||
      f.partnerPkid !== null ||
      f.courseGroupPkid !== null ||
      f.publishStatusPkid !== null ||
      f.canRepeat !== null ||
      f.scheduleOnFrom !== null ||
      f.scheduleOnTo !== null ||
      f.scheduleOffFrom !== null ||
      f.scheduleOffTo !== null
    );
  }

  protected load(): void {
    this.loading.set(true);

    this.service.query(this.filters).subscribe({
      next: (courses) => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.courses.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得課程資料。',
        });
      },
    });
  }

  protected openDrawer(): void {
    this.draftFilters = toDraft(this.filters);
    this.drawerVisible.set(true);
  }

  protected applyFilters(): void {
    this.filters = toQuery(this.draftFilters);
    this.page = { ...this.page, first: 0 };

    this.listState.write(FILTERS_KEY, this.filters);
    this.listState.write(PAGE_KEY, this.page);

    this.drawerVisible.set(false);
    this.load();
  }

  protected resetFilters(): void {
    this.draftFilters = toDraft(EMPTY_COURSE_QUERY);
    this.filters = { ...EMPTY_COURSE_QUERY };
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

  protected view(course: Course): void {
    void this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    void this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  protected add(): void {
    void this.router.navigate(['/courses', 'new']);
  }

  protected confirmDelete(course: Course): void {
    this.confirmationService.confirm({
      header: '刪除課程',
      message: `確定要刪除主代碼 ${course.pkid}「${course.title}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(course),
    });
  }

  /** The three FK lookups feed the drawer only; the table labels come from the row nav objects. */
  private loadLookups(): void {
    forkJoin({
      partners: this.lookupService.getPartners(),
      courseGroups: this.lookupService.getCourseGroups(),
      publishStatuses: this.lookupService.getPublishStatuses(),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
      },
      error: () =>
        this.messageService.add({
          severity: 'warn',
          summary: '搜尋選項載入失敗',
          detail: '原廠／課程群組／上架狀態的選項暫時無法使用。',
        }),
    });
  }

  private delete(course: Course): void {
    this.service.delete(course.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: `課程「${course.title}」已刪除。`,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error?.status === 409
              ? '此課程已被課程問答、相關連結或熱門課程使用，無法刪除。'
              : '請稍後再試一次。',
        }),
    });
  }
}

/** Query (ISO dates) → drawer draft (`Date`s). */
function toDraft(query: CourseQuery): DraftFilters {
  return {
    keyword: query.keyword ?? null,
    partnerPkid: query.partnerPkid ?? null,
    courseGroupPkid: query.courseGroupPkid ?? null,
    publishStatusPkid: query.publishStatusPkid ?? null,
    canRepeat: query.canRepeat ?? null,
    scheduleOnFrom: fromIso(query.scheduleOnFrom),
    scheduleOnTo: fromIso(query.scheduleOnTo),
    scheduleOffFrom: fromIso(query.scheduleOffFrom),
    scheduleOffTo: fromIso(query.scheduleOffTo),
  };
}

/** Drawer draft (`Date`s) → query (ISO dates). */
function toQuery(draft: DraftFilters): CourseQuery {
  return {
    keyword: draft.keyword?.trim() || null,
    partnerPkid: draft.partnerPkid,
    courseGroupPkid: draft.courseGroupPkid,
    publishStatusPkid: draft.publishStatusPkid,
    canRepeat: draft.canRepeat,
    scheduleOnFrom: toIso(draft.scheduleOnFrom),
    scheduleOnTo: toIso(draft.scheduleOnTo),
    scheduleOffFrom: toIso(draft.scheduleOffFrom),
    scheduleOffTo: toIso(draft.scheduleOffTo),
  };
}
