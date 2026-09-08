import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, ElementRef, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { forkJoin, switchMap } from 'rxjs';

import {
  Course,
  CourseGroupLookup,
  CourseQuery,
  CourseUpdateRequest,
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

/**
 * The list columns that can be edited in place. 主代碼 (the IDENTITY key) and the two FK label
 * columns 原廠 / 課程群組 are deliberately absent — those stay read-only on this page.
 */
export type EditableField =
  | 'displayOrder'
  | 'courseId'
  | 'prodCourseId'
  | 'title'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

const EDITABLE_FIELDS: readonly string[] = [
  'displayOrder',
  'courseId',
  'prodCourseId',
  'title',
  'publishStatusPkid',
  'scheduleOn',
  'scheduleOff',
  'hour',
  'listPrice',
  'learningCredit',
  'canRepeat',
];

/** Everything a cell editor can hold: text, number, `bit`, or a `date` as a `Date`. */
type EditValue = string | number | boolean | Date | null;

/** `maxlength` per editable text column, matching the SQL widths the form already enforces. */
const TEXT_MAX_LENGTH: Record<'title' | 'courseId' | 'prodCourseId', number> = {
  title: 200,
  courseId: 50,
  prodCourseId: 50,
};

interface SortState {
  field: string;
  order: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** The cell currently open for editing, plus the value to restore if the save fails. */
interface EditingCell {
  pkid: number;
  field: EditableField;
  /** The row's value when editing started. */
  original: EditValue;
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

/** 課程 Course — list page with a filter drawer, sorting, paging and in-place cell editing. */
@Component({
  selector: 'app-course-list',
  imports: [
    DecimalPipe,
    NgTemplateOutlet,
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DrawerModule,
    InputNumberModule,
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
  private readonly host = inject(ElementRef<HTMLElement>);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly drawerVisible = signal(false);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatus[]>([]);

  /** The cell open for editing, or null when the table is read-only. */
  protected readonly editingCell = signal<EditingCell | null>(null);
  /** The open editor's working value; committed on blur, discarded on Esc. */
  protected editValue: EditValue = null;
  /** Validation message for the open cell — set on a failed commit, which keeps the cell open. */
  protected readonly editError = signal<string | null>(null);
  /** True while an inline save is in flight, so a second blur cannot fire a second PUT. */
  protected readonly savingCell = signal(false);

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

  /** 上架狀態 is required, so its cell editor carries no 不限／無 entry. */
  protected get publishStatusEditOptions(): SelectOption<number>[] {
    return this.publishStatuses().map((s) => ({ label: s.description, value: s.pkid }));
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
        this.closeEditor();
        this.loading.set(false);
      },
      error: () => {
        this.courses.set([]);
        this.closeEditor();
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得課程資料。',
        });
      },
    });
  }

  // ------------------------------------------------------------------ in-place cell editing

  /** Guards every entry point: only the eleven editable columns may open an editor. */
  protected isEditable(field: string): field is EditableField {
    return EDITABLE_FIELDS.includes(field);
  }

  protected isEditing(course: Course, field: EditableField): boolean {
    const cell = this.editingCell();
    return cell !== null && cell.pkid === course.pkid && cell.field === field;
  }

  /**
   * Opens a cell editor. Bound to `dblclick` only — a single click never starts editing, which is
   * why PrimeNG's own `pEditableColumn` (a click-to-open directive) is not used here.
   */
  protected startEdit(course: Course, field: EditableField): void {
    if (!this.isEditable(field) || this.savingCell()) return;

    const original = readField(course, field);

    this.editingCell.set({ pkid: course.pkid, field, original });
    this.editValue = original;
    this.editError.set(null);
    this.focusEditor();
  }

  /** Esc — throws the working value away and leaves the row as it was. */
  protected cancelEdit(): void {
    this.closeEditor();
  }

  /**
   * Blur (and Enter) commit the cell. An invalid value keeps the editor open with an inline error;
   * a valid one is persisted through the existing PUT /api/courses.
   */
  protected commitEdit(): void {
    const cell = this.editingCell();
    if (cell === null || this.savingCell()) return;

    const course = this.courses().find((c) => c.pkid === cell.pkid);
    if (!course) {
      this.closeEditor();
      return;
    }

    const error = validateField(cell.field, this.editValue, course);
    if (error !== null) {
      // Stay in edit mode so the user can correct the value in place.
      this.editError.set(error);
      return;
    }

    const value = normaliseField(cell.field, this.editValue);
    if (isSameValue(value, cell.original)) {
      this.closeEditor();
      return;
    }

    this.save(cell, value);
  }

  /**
   * The list projection carries no n-n keys, and the API's PUT replaces both junctions from the
   * request — so the full record is re-read first and the edited field merged into it. Sending a
   * list row straight back would wipe the course's 職務類別 and 對應認證 links.
   */
  private save(cell: EditingCell, value: EditValue): void {
    this.savingCell.set(true);

    this.service
      .getById(cell.pkid)
      .pipe(switchMap((full) => this.service.update(toUpdateRequest(full, cell.field, value))))
      .subscribe({
        next: (saved) => {
          this.savingCell.set(false);
          this.closeEditor();
          this.courses.update((rows) => rows.map((r) => (r.pkid === saved.pkid ? saved : r)));
          this.messageService.add({
            severity: 'success',
            summary: '已更新',
            detail: `課程「${saved.title}」已儲存。`,
          });
        },
        error: () => {
          this.savingCell.set(false);
          // Revert: the row itself was never mutated, so restoring the working value and closing
          // the editor puts the previous value back on screen.
          this.editValue = cell.original;
          this.closeEditor();
          this.messageService.add({
            severity: 'error',
            summary: '儲存失敗',
            detail: `「${labelOf(cell.field)}」未更新，已還原原本的值。`,
          });
        },
      });
  }

  private closeEditor(): void {
    this.editingCell.set(null);
    this.editError.set(null);
  }

  /** Puts the caret in the freshly rendered editor; the `@if` has not painted yet on this tick. */
  private focusEditor(): void {
    setTimeout(() => {
      const host = this.host.nativeElement as HTMLElement;
      const editor = host.querySelector<HTMLElement>(
        '.course-list__cell--editing input, .course-list__cell--editing [tabindex]',
      );
      editor?.focus();
    });
  }

  // ------------------------------------------------------------------ filters, sorting, paging

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

  /** The three FK lookups feed the drawer, and 上架狀態 also feeds its cell editor. */
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

/** Column headings, reused in the revert message so the user knows which cell bounced back. */
function labelOf(field: EditableField): string {
  const labels: Record<EditableField, string> = {
    displayOrder: '顯示順序',
    courseId: '簡介代碼',
    prodCourseId: '科目代碼',
    title: '課程名稱',
    publishStatusPkid: '上架狀態',
    scheduleOn: '上架日期',
    scheduleOff: '下架日期',
    hour: '時數',
    listPrice: '定價',
    learningCredit: '點數',
    canRepeat: '允許重聽',
  };
  return labels[field];
}

/** Row value → editor value. The two `date` columns travel as ISO strings and edit as `Date`s. */
function readField(course: Course, field: EditableField): EditValue {
  if (field === 'scheduleOn') return fromIso(course.scheduleOn);
  if (field === 'scheduleOff') return fromIso(course.scheduleOff);
  return course[field];
}

/** True only for a `Date` that actually parsed — `new Date('nope')` is a Date too. */
function isValidDate(value: unknown): value is Date {
  return value instanceof Date && !Number.isNaN(value.getTime());
}

/**
 * Validates one cell against the same rules the form enforces: required columns cannot be cleared,
 * the numeric columns must be non-negative numbers, both dates must parse, and 上架日期 must not
 * fall after 下架日期 — the sibling bound comes from the row being edited.
 */
function validateField(field: EditableField, value: EditValue, row: Course): string | null {
  const required = '此欄位必填，不可清空。';
  const outOfOrder = '上架日期不可晚於下架日期。';

  switch (field) {
    case 'title':
    case 'courseId':
    case 'prodCourseId': {
      const text = typeof value === 'string' ? value.trim() : '';
      if (text === '') return required;
      if (text.length > TEXT_MAX_LENGTH[field]) return `不可超過 ${TEXT_MAX_LENGTH[field]} 個字元。`;
      return null;
    }

    case 'displayOrder':
    case 'hour':
    case 'listPrice':
    case 'learningCredit': {
      if (value === null || value === '') return required;
      const num = typeof value === 'number' ? value : Number(value);
      if (!Number.isFinite(num)) return '請輸入有效的數字。';
      if (num < 0) return '不可小於 0。';
      return null;
    }

    case 'publishStatusPkid':
      return value === null || value === '' ? required : null;

    case 'scheduleOn': {
      if (value === null || value === '') return required;
      if (!isValidDate(value)) return '請輸入有效的日期。';

      const scheduleOff = fromIso(row.scheduleOff);
      return scheduleOff && value.getTime() > scheduleOff.getTime() ? outOfOrder : null;
    }

    case 'scheduleOff': {
      if (value === null || value === '') return required;
      if (!isValidDate(value)) return '請輸入有效的日期。';

      const scheduleOn = fromIso(row.scheduleOn);
      return scheduleOn && scheduleOn.getTime() > value.getTime() ? outOfOrder : null;
    }

    case 'canRepeat':
      // A bit column is always in one of its two valid states.
      return null;
  }
}

/** Editor value → the value that goes on the wire: text is trimmed, numbers are coerced. */
function normaliseField(field: EditableField, value: EditValue): EditValue {
  switch (field) {
    case 'title':
    case 'courseId':
    case 'prodCourseId':
      return typeof value === 'string' ? value.trim() : value;

    case 'displayOrder':
    case 'hour':
    case 'listPrice':
    case 'learningCredit':
    case 'publishStatusPkid':
      return typeof value === 'number' ? value : Number(value);

    default:
      return value;
  }
}

/** Change detection for a committed cell — `Date`s compare by timestamp, not by identity. */
function isSameValue(next: EditValue, previous: EditValue): boolean {
  if (next instanceof Date && previous instanceof Date) {
    return next.getTime() === previous.getTime();
  }
  return next === previous;
}

/** Full record + the one edited field → the PUT body, n-n keys carried over untouched. */
function toUpdateRequest(
  course: Course,
  field: EditableField,
  value: EditValue,
): CourseUpdateRequest {
  const request: CourseUpdateRequest = {
    pkid: course.pkid,
    title: course.title,
    officialTitle: course.officialTitle,
    courseId: course.courseId,
    prodCourseId: course.prodCourseId,
    friendlyUrl: course.friendlyUrl,
    displayOrder: course.displayOrder,
    partnerPkid: course.partnerPkid,
    courseGroupPkid: course.courseGroupPkid,
    publishStatusPkid: course.publishStatusPkid,
    scheduleOn: course.scheduleOn,
    scheduleOff: course.scheduleOff,
    hour: course.hour,
    listPrice: course.listPrice,
    learningCredit: course.learningCredit,
    material: course.material,
    objective: course.objective,
    target: course.target,
    prerequisites: course.prerequisites,
    outline: course.outline,
    towardCertOrExam: course.towardCertOrExam,
    note: course.note,
    otherInfo: course.otherInfo,
    canRepeat: course.canRepeat,
    jobCategoryPkids: course.jobCategories.map((jc) => jc.pkid),
    certificationPkids: course.certifications.map((ct) => ct.pkid),
  };

  if (field === 'scheduleOn' || field === 'scheduleOff') {
    // validateField has already rejected a null or unparseable date.
    return { ...request, [field]: toIso(value as Date) as string };
  }

  // readField and validateField together prove the field/value pair is compatible.
  return { ...request, [field]: value } as CourseUpdateRequest;
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
