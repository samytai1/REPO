import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { Course } from '@core/models';
import { CourseList } from './course-list';

// Regression: ISSUE-001 — sort and page changes were never persisted. `(onSort)` emits
//   `{ field, order }` and `(onPage)` emits `{ first, rows }`, but `onStateChange` only read the
//   `TableLazyLoadEvent` names, so `course-list-sort` was never written and every sort overwrote
//   `course-list-page` with `first: 0`. The unit spec fed the lazy-load shape and never saw it, so
//   this one drives the real header and paginator instead.
// Regression: ISSUE-002 — a stored page was never restored: p-table re-emits `(onSort)` with its
//   current sort every time the value is assigned, and that init-time re-emit was treated as a
//   user sort that reset the page to 0 before the rows had even rendered.
// Found by /qa on 2026-09-10
// Report: .gstack/qa-reports/qa-report-localhost-4200-2026-09-10.md

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  sort: { field: string; order: number };
  page: { first: number; rows: number };
};

describe('CourseList (regression ISSUE-001: sort/page persistence from real p-table events)', () => {
  const queryUrl = `${environment.apiBaseUrl}/courses/query`;
  const partnersUrl = `${environment.apiBaseUrl}/lookups/partners`;
  const courseGroupsUrl = `${environment.apiBaseUrl}/lookups/course-groups`;
  const publishStatusesUrl = `${environment.apiBaseUrl}/lookups/publish-statuses`;

  const base: Course = {
    pkid: 1,
    title: 'Azure 基礎架構',
    officialTitle: null,
    courseId: 'AZ-104',
    prodCourseId: 'MS-AZ104',
    friendlyUrl: 'azure-admin',
    displayOrder: 0,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 30,
    listPrice: 24000,
    learningCredit: 30,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: '微軟' },
    courseGroup: null,
    publishStatus: { pkid: 2, description: '已發布' },
    jobCategoryCount: 0,
    certificationCount: 0,
    jobCategories: [],
    certifications: [],
  };

  /** 25 rows — enough for a second page at the default 20 rows, so the paginator can move. */
  const courses: Course[] = Array.from({ length: 25 }, (_, i) => ({
    ...base,
    pkid: i + 1,
    title: `課程 ${String(i + 1).padStart(2, '0')}`,
    courseId: `C-${i + 1}`,
    displayOrder: i,
  }));

  let fixture: ComponentFixture<CourseList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;

  function flushLookups(): void {
    httpMock.expectOne(partnersUrl).flush([
      {
        pkid: 1,
        name: '微軟',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 20,
        imageFilename: null,
      },
    ]);
    httpMock.expectOne(courseGroupsUrl).flush([{ pkid: 10, description: '雲端' }]);
    httpMock.expectOne(publishStatusesUrl).flush([
      { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
    ]);
  }

  /** Answers the initial POST /query (and the lookups) that ngOnInit fires. */
  function flushInitialLoad(): void {
    flushLookups();
    httpMock.expectOne(queryUrl).flush(courses);
    fixture.detectChanges();
  }

  function storedSort(): unknown {
    return JSON.parse(sessionStorage.getItem('course-list-sort') ?? 'null');
  }

  function storedPage(): unknown {
    return JSON.parse(sessionStorage.getItem('course-list-page') ?? 'null');
  }

  function clickHeader(field: string): void {
    const header = fixture.nativeElement.querySelector(
      `th[psortablecolumn="${field}"]`,
    ) as HTMLElement | null;
    expect(header).withContext(`a sortable ${field} header`).not.toBeNull();
    header!.click();
    fixture.detectChanges();
  }

  function clickNextPage(): void {
    const next = fixture.nativeElement.querySelector(
      'button[aria-label="Next Page"]',
    ) as HTMLButtonElement | null;
    expect(next).withContext('the paginator next button').not.toBeNull();
    next!.click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('writes course-list-sort when a column header is clicked — the real (onSort) payload', () => {
    flushInitialLoad();
    expect(storedSort()).withContext('nothing stored before the click').toBeNull();

    clickHeader('title');

    // First click on a fresh column sorts ascending (p-table's defaultSortOrder).
    expect(storedSort()).toEqual({ field: 'title', order: 1 });
    expect(component.sort).toEqual({ field: 'title', order: 1 });

    clickHeader('title');

    expect(storedSort()).toEqual({ field: 'title', order: -1 });
  });

  it('writes course-list-page when the paginator moves — the real (onPage) payload', () => {
    flushInitialLoad();

    clickNextPage();

    expect(storedPage()).toEqual({ first: 20, rows: 20 });
    expect(component.page).toEqual({ first: 20, rows: 20 });
    // A page change alone must not invent or clear a sort.
    expect(storedSort()).toBeNull();
  });

  it('a sort after paging starts from page 1 without losing the rows-per-page setting', () => {
    flushInitialLoad();
    clickNextPage();
    expect(storedPage()).toEqual({ first: 20, rows: 20 });

    clickHeader('title');

    // The table's own resetPageOnSort goes back to the first page; the stored page must agree,
    // and the 20-per-page choice must survive the event that carried no `rows`.
    expect(storedPage()).toEqual({ first: 0, rows: 20 });
    expect(storedSort()).toEqual({ field: 'title', order: 1 });
  });

  it('does not treat the init-time (onSort) re-emit as a user sort', () => {
    flushInitialLoad();

    // The table has sorted by the default column on load, which re-emitted (onSort); a sort that
    // matches what is already applied is not a user action and must not be written.
    expect(storedSort()).toBeNull();
    expect(storedPage()).toEqual({ first: 0, rows: 20 });
  });

  it('restores a stored page offset instead of losing it to the init-time (onSort) re-emit', () => {
    // Settle the beforeEach fixture's requests first so the second component's are unambiguous.
    flushInitialLoad();

    sessionStorage.setItem('course-list-page', JSON.stringify({ first: 20, rows: 20 }));

    const restored = TestBed.createComponent(CourseList);
    const restoredComponent = restored.componentInstance as unknown as ListInternals;
    restored.detectChanges();
    flushLookups();
    httpMock.expectOne(queryUrl).flush(courses);
    restored.detectChanges();

    expect(restoredComponent.page).toEqual({ first: 20, rows: 20 });
    expect(storedPage()).toEqual({ first: 20, rows: 20 });
    // And the table really is on page 2: rows 21–25 of 25, first row 課程 21.
    expect(restored.nativeElement.querySelector('.p-paginator-current').textContent).toContain(
      '21–25',
    );
    expect(
      restored.nativeElement.querySelector('tbody tr td[data-field="title"]').textContent,
    ).toContain('課程 21');
  });
});
