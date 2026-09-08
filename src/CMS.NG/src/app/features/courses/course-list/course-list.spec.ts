import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { environment } from '@environments/environment';

import { Course, CourseQuery } from '@core/models';
import { CourseList } from './course-list';

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  courses: () => Course[];
  loading: () => boolean;
  drawerVisible: () => boolean;
  filters: CourseQuery;
  draftFilters: {
    keyword: string | null;
    partnerPkid: number | null;
    courseGroupPkid: number | null;
    publishStatusPkid: number | null;
    canRepeat: boolean | null;
    scheduleOnFrom: Date | null;
    scheduleOnTo: Date | null;
    scheduleOffFrom: Date | null;
    scheduleOffTo: Date | null;
  };
  sort: { field: string; order: number };
  page: { first: number; rows: number };
  hasActiveFilters: boolean;
  partnerOptions: { label: string; value: number | null }[];
  courseGroupOptions: { label: string; value: number | null }[];
  publishStatusOptions: { label: string; value: number | null }[];
  openDrawer(): void;
  applyFilters(): void;
  resetFilters(): void;
  onStateChange(event: {
    first?: number;
    rows?: number;
    sortField?: string;
    sortOrder?: number;
  }): void;
  view(course: Course): void;
  edit(course: Course): void;
  add(): void;
  confirmDelete(course: Course): void;
  // In-place editing. `field` is widened to string here so the spec can also probe the
  // read-only columns, which the EditableField union deliberately excludes.
  editingCell: () => { pkid: number; field: string; original: unknown } | null;
  editValue: unknown;
  editError: () => string | null;
  savingCell: () => boolean;
  publishStatusEditOptions: { label: string; value: number }[];
  isEditable(field: string): boolean;
  isEditing(course: Course, field: string): boolean;
  startEdit(course: Course, field: string): void;
  commitEdit(): void;
  cancelEdit(): void;
};

describe('CourseList', () => {
  const queryUrl = `${environment.apiBaseUrl}/courses/query`;
  const partnersUrl = `${environment.apiBaseUrl}/lookups/partners`;
  const courseGroupsUrl = `${environment.apiBaseUrl}/lookups/course-groups`;
  const publishStatusesUrl = `${environment.apiBaseUrl}/lookups/publish-statuses`;

  const azure: Course = {
    pkid: 1,
    title: 'Azure 基礎架構',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'MS-AZ104',
    friendlyUrl: 'azure-admin',
    displayOrder: 20,
    partnerPkid: 1,
    courseGroupPkid: 10,
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
    courseGroup: { pkid: 10, description: '雲端' },
    publishStatus: { pkid: 2, description: '已發布' },
    jobCategoryCount: 1,
    certificationCount: 1,
    jobCategories: [],
    certifications: [],
  };

  const ccna: Course = {
    ...azure,
    pkid: 2,
    title: 'CCNA 網路實務',
    officialTitle: null,
    courseId: 'CCNA-200',
    prodCourseId: 'CI-CCNA',
    friendlyUrl: 'ccna',
    displayOrder: 10,
    partnerPkid: 2,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-03-01',
    scheduleOff: '2030-03-01',
    hour: 40,
    listPrice: 32000,
    learningCredit: 40.5,
    canRepeat: false,
    partner: { pkid: 2, name: '思科' },
    courseGroup: null,
    publishStatus: { pkid: 1, description: '草稿' },
    jobCategoryCount: 0,
    certificationCount: 0,
  };

  const courses: Course[] = [ccna, azure];

  let fixture: ComponentFixture<CourseList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Answers the three lookup GETs that ngOnInit fires alongside the query. */
  function flushLookups(): void {
    httpMock.expectOne(partnersUrl).flush([
      {
        pkid: 2,
        name: '思科',
        appKey: 'CISCO',
        nameOnPartnerMenu: 'Cisco',
        nameOnCourseDetailPage: '思科',
        displayOrder: 10,
        imageFilename: null,
      },
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
      { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
      { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
    ]);
  }

  /** Answers the initial POST /query (and the lookups) that ngOnInit fires. */
  function flushInitialLoad(payload: Course[] = courses): void {
    flushLookups();
    httpMock.expectOne(queryUrl).flush(payload);
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
    router = TestBed.inject(Router);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates and loads courses through POST /api/courses/query', () => {
    flushInitialLoad();

    expect(component.courses().length).toBe(2);
    expect(component.loading()).toBeFalse();
  });

  it('loads the three filter lookups and prepends a 不限 option to each', () => {
    flushInitialLoad();

    expect(component.partnerOptions[0]).toEqual({ label: '不限', value: null });
    expect(component.partnerOptions.map((o) => o.label)).toEqual(['不限', '思科', '微軟']);
    expect(component.courseGroupOptions.map((o) => o.label)).toEqual(['不限', '雲端']);
    expect(component.publishStatusOptions.map((o) => o.label)).toEqual(['不限', '草稿', '已發布']);
  });

  it('keeps the list usable when a lookup fails', () => {
    // forkJoin cancels its siblings the moment one errors, so answer the good ones first.
    httpMock.expectOne(courseGroupsUrl).flush([]);
    httpMock.expectOne(publishStatusesUrl).flush([]);
    httpMock.expectOne(partnersUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne(queryUrl).flush(courses);
    fixture.detectChanges();

    expect(component.courses().length).toBe(2);
    expect(component.partnerOptions.length).toBe(1);
  });

  it('renders a row per course', () => {
    flushInitialLoad();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Azure 基礎架構');
    expect(fixture.nativeElement.textContent).toContain('CCNA 網路實務');
  });

  it('renders every requested column of a row, including the FK labels', () => {
    flushInitialLoad([azure]);

    const cells = Array.from(
      fixture.nativeElement.querySelectorAll('tbody tr:first-child td') as NodeListOf<HTMLElement>,
    ).map((td) => td.textContent?.trim());

    expect(cells.slice(0, 14)).toEqual([
      '1',
      '20',
      'AZ-104',
      'MS-AZ104',
      'Azure 基礎架構',
      '微軟',
      '雲端',
      '已發布',
      '2026-01-01',
      '2036-01-01',
      '30',
      '24,000',
      '30.0',
      '是',
    ]);
  });

  it('falls back to a dash when a course has no course group', () => {
    flushInitialLoad([ccna]);

    const cells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(cells[6].textContent.trim()).toBe('—');
  });

  it('renders 允許重聽 as a 是／否 tag, never a raw boolean', () => {
    flushInitialLoad([ccna]);

    const cells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(cells[13].querySelector('p-tag')).not.toBeNull();
    expect(cells[13].textContent.trim()).toBe('否');
    expect(fixture.nativeElement.textContent).not.toContain('false');
  });

  it('defaults the sort to DisplayOrder ascending', () => {
    flushInitialLoad();

    expect(component.sort).toEqual({ field: 'displayOrder', order: 1 });
  });

  it('shows the empty message when the API returns nothing', () => {
    flushInitialLoad([]);

    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('clears the list and stops loading when the request fails', () => {
    flushLookups();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.courses()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  it('sends the drawer filters on the next query, serialising dates as ISO', () => {
    flushInitialLoad();

    component.openDrawer();
    component.draftFilters.keyword = ' Azure ';
    component.draftFilters.partnerPkid = 1;
    component.draftFilters.courseGroupPkid = 10;
    component.draftFilters.publishStatusPkid = 2;
    component.draftFilters.canRepeat = true;
    component.draftFilters.scheduleOnFrom = new Date(2026, 0, 1);
    component.draftFilters.scheduleOffTo = new Date(2040, 11, 31, 23, 30);
    component.applyFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({
      keyword: 'Azure',
      partnerPkid: 1,
      courseGroupPkid: 10,
      publishStatusPkid: 2,
      canRepeat: true,
      scheduleOnFrom: '2026-01-01',
      scheduleOnTo: null,
      scheduleOffFrom: null,
      scheduleOffTo: '2040-12-31',
    });
    req.flush([azure]);

    expect(component.courses().length).toBe(1);
    expect(component.drawerVisible()).toBeFalse();
    expect(component.hasActiveFilters).toBeTrue();
  });

  it('treats a false canRepeat filter as active, not as "unset"', () => {
    flushInitialLoad();

    component.draftFilters.canRepeat = false;
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([ccna]);

    expect(component.hasActiveFilters).toBeTrue();
  });

  it('persists the applied filters to session storage', () => {
    flushInitialLoad();

    component.draftFilters.keyword = 'Azure';
    component.draftFilters.scheduleOnFrom = new Date(2026, 0, 1);
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([azure]);

    const stored = JSON.parse(sessionStorage.getItem('course-list-filters') ?? '{}');
    expect(stored.keyword).toBe('Azure');
    expect(stored.scheduleOnFrom).toBe('2026-01-01');
  });

  it('restores persisted filters on init and rehydrates the drawer dates', () => {
    flushInitialLoad();

    sessionStorage.setItem(
      'course-list-filters',
      JSON.stringify({ keyword: 'restored', canRepeat: false, scheduleOnFrom: '2026-01-01' }),
    );
    sessionStorage.setItem('course-list-sort', JSON.stringify({ field: 'title', order: -1 }));
    sessionStorage.setItem('course-list-page', JSON.stringify({ first: 0, rows: 50 }));

    const restored = TestBed.createComponent(CourseList);
    const restoredComponent = restored.componentInstance as unknown as ListInternals;
    restored.detectChanges();

    expect(restoredComponent.filters.keyword).toBe('restored');
    expect(restoredComponent.filters.canRepeat).toBeFalse();
    expect(restoredComponent.filters.scheduleOnFrom).toBe('2026-01-01');
    // A filter absent from the stored shape still reads as null, not undefined.
    expect(restoredComponent.filters.partnerPkid).toBeNull();
    expect(restoredComponent.draftFilters.scheduleOnFrom?.getFullYear()).toBe(2026);
    expect(restoredComponent.sort).toEqual({ field: 'title', order: -1 });
    expect(restoredComponent.page).toEqual({ first: 0, rows: 50 });

    flushLookups();
    httpMock.expectOne(queryUrl).flush(courses);
  });

  it('resets the filters and reloads', () => {
    flushInitialLoad();

    component.draftFilters.keyword = 'Azure';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([azure]);

    component.resetFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({
      keyword: null,
      partnerPkid: null,
      courseGroupPkid: null,
      publishStatusPkid: null,
      canRepeat: null,
      scheduleOnFrom: null,
      scheduleOnTo: null,
      scheduleOffFrom: null,
      scheduleOffTo: null,
    });
    req.flush(courses);

    expect(component.hasActiveFilters).toBeFalse();
    expect(sessionStorage.getItem('course-list-filters')).toBeNull();
  });

  it('persists sort and page changes', () => {
    flushInitialLoad();

    component.onStateChange({ first: 20, rows: 20, sortField: 'title', sortOrder: -1 });

    expect(JSON.parse(sessionStorage.getItem('course-list-sort') ?? '{}')).toEqual({
      field: 'title',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('course-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('navigates to the detail, edit and add pages', () => {
    flushInitialLoad();
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    component.view(azure);
    expect(navigate).toHaveBeenCalledWith(['/courses', 1]);

    component.edit(azure);
    expect(navigate).toHaveBeenCalledWith(['/courses', 1, 'edit']);

    component.add();
    expect(navigate).toHaveBeenCalledWith(['/courses', 'new']);
  });

  it('deletes only after the confirmation is accepted, then reloads', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    component.confirmDelete(ccna);

    const deleteReq = httpMock.expectOne(`${environment.apiBaseUrl}/courses/2`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([azure]);
    expect(component.courses().length).toBe(1);
  });

  it('names the record in the delete confirmation', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(azure);

    expect(confirm.calls.mostRecent().args[0].message).toContain('主代碼 1「Azure 基礎架構」');
  });

  it('reports a 409 delete as a still-referenced course', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    const add = spyOn(messageService, 'add');

    component.confirmDelete(azure);

    httpMock
      .expectOne(`${environment.apiBaseUrl}/courses/1`)
      .flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(add.calls.mostRecent().args[0].detail).toContain('已被課程問答、相關連結或熱門課程使用');
    expect(component.courses().length).toBe(2);
  });

  it('does not delete when the confirmation is dismissed', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(azure);

    httpMock.expectNone(`${environment.apiBaseUrl}/courses/1`);
    expect(component.courses().length).toBe(2);
  });

  describe('in-place editing', () => {
    const coursesUrl = `${environment.apiBaseUrl}/courses`;
    const azureUrl = `${coursesUrl}/1`;

    /** What GET /api/courses/1 returns: the list row plus the two n-n collections. */
    const azureFull: Course = {
      ...azure,
      jobCategories: [{ pkid: 5, description: '雲端工程師' }],
      certifications: [{ pkid: 7, title: 'AZ-104', partnerPkid: 1 }],
    };

    /** The first rendered row is `azure`; every spec here loads it alone. */
    function cell(field: string): HTMLElement {
      return fixture.nativeElement.querySelector(
        `tbody tr:first-child td[data-field="${field}"]`,
      ) as HTMLElement;
    }

    function dispatchOn(field: string, type: 'click' | 'dblclick'): HTMLElement {
      const td = cell(field);
      td.dispatchEvent(new MouseEvent(type, { bubbles: true }));
      fixture.detectChanges();
      return td;
    }

    /** Opens an editor through the component, the way a double-click does. */
    function openEditor(field: string, value: unknown): void {
      component.startEdit(azure, field);
      component.editValue = value;
      fixture.detectChanges();
    }

    /** Answers the re-read + PUT that a committed cell fires, and returns the PUT body. */
    function flushSave(saved: Course = azureFull): Record<string, unknown> {
      const read = httpMock.expectOne(azureUrl);
      expect(read.request.method).toBe('GET');
      read.flush(azureFull);

      const put = httpMock.expectOne(coursesUrl);
      expect(put.request.method).toBe('PUT');

      const body = put.request.body as Record<string, unknown>;
      put.flush(saved);
      fixture.detectChanges();

      return body;
    }

    it('opens a cell editor on double-click', () => {
      flushInitialLoad([azure]);

      dispatchOn('title', 'dblclick');

      expect(component.editingCell()).toEqual(
        jasmine.objectContaining({ pkid: 1, field: 'title' }),
      );
      expect(cell('title').querySelector('input')).not.toBeNull();
    });

    it('does not start editing on a single click', () => {
      flushInitialLoad([azure]);

      dispatchOn('title', 'click');

      expect(component.editingCell()).toBeNull();
      expect(cell('title').querySelector('input')).toBeNull();
      expect(cell('title').textContent?.trim()).toBe('Azure 基礎架構');
    });

    it('leaves 主代碼, 原廠 and 課程群組 read-only', () => {
      flushInitialLoad([azure]);

      for (const field of ['pkid', 'partner', 'courseGroup']) {
        dispatchOn(field, 'dblclick');

        expect(component.editingCell()).withContext(field).toBeNull();
        expect(cell(field).querySelector('input')).withContext(field).toBeNull();
      }

      expect(component.isEditable('pkid')).toBeFalse();
      expect(component.isEditable('partner')).toBeFalse();
      expect(component.isEditable('courseGroup')).toBeFalse();
    });

    it('refuses to open an editor for a read-only field even when asked directly', () => {
      flushInitialLoad([azure]);

      component.startEdit(azure, 'pkid');
      component.startEdit(azure, 'partner');

      expect(component.editingCell()).toBeNull();
    });

    it('opens the matching widget per column', () => {
      flushInitialLoad([azure]);

      dispatchOn('hour', 'dblclick');
      expect(cell('hour').querySelector('p-inputnumber')).not.toBeNull();

      dispatchOn('publishStatusPkid', 'dblclick');
      expect(cell('publishStatusPkid').querySelector('p-select')).not.toBeNull();

      dispatchOn('scheduleOn', 'dblclick');
      expect(cell('scheduleOn').querySelector('p-datepicker')).not.toBeNull();

      dispatchOn('canRepeat', 'dblclick');
      expect(cell('canRepeat').querySelector('p-checkbox')).not.toBeNull();

      component.cancelEdit();
      fixture.detectChanges();
    });

    it('offers 上架狀態 without a 不限 entry — the column is required', () => {
      flushInitialLoad([azure]);

      expect(component.publishStatusEditOptions.map((o) => o.label)).toEqual(['草稿', '已發布']);
    });

    it('persists through GET then PUT /api/courses when the editor loses focus', () => {
      flushInitialLoad([azure]);

      dispatchOn('title', 'dblclick');
      const input = cell('title').querySelector('input') as HTMLInputElement;
      input.value = '  Azure 進階架構  ';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      const body = flushSave({ ...azureFull, title: 'Azure 進階架構' });

      expect(body['pkid']).toBe(1);
      expect(body['title']).toBe('Azure 進階架構');
      expect(component.editingCell()).toBeNull();
      expect(component.courses()[0].title).toBe('Azure 進階架構');
      expect(cell('title').textContent?.trim()).toBe('Azure 進階架構');
    });

    it('re-reads the record first so the inline PUT keeps the n-n keys', () => {
      flushInitialLoad([azure]);

      openEditor('hour', 45);
      component.commitEdit();

      const body = flushSave({ ...azureFull, hour: 45 });

      expect(body['hour']).toBe(45);
      // The list projection has no n-n arrays, and the API's PUT replaces both junctions.
      expect(body['jobCategoryPkids']).toEqual([5]);
      expect(body['certificationPkids']).toEqual([7]);
      // Columns the list never shows still travel unchanged.
      expect(body['friendlyUrl']).toBe('azure-admin');
      expect(body['officialTitle']).toBe('Microsoft Azure Administrator');
    });

    it('sends a date column as an ISO string in local time', () => {
      flushInitialLoad([azure]);

      openEditor('scheduleOn', new Date(2027, 1, 3, 23, 30));
      component.commitEdit();

      const body = flushSave({ ...azureFull, scheduleOn: '2027-02-03' });

      expect(body['scheduleOn']).toBe('2027-02-03');
      expect(body['scheduleOff']).toBe('2036-01-01');
    });

    it('sends the 上架狀態 dropdown and re-renders the saved label', () => {
      flushInitialLoad([azure]);

      openEditor('publishStatusPkid', 1);
      component.commitEdit();

      const body = flushSave({
        ...azureFull,
        publishStatusPkid: 1,
        publishStatus: { pkid: 1, description: '草稿' },
      });

      expect(body['publishStatusPkid']).toBe(1);
      expect(cell('publishStatusPkid').textContent?.trim()).toBe('草稿');
    });

    it('sends the 允許重聽 checkbox, including a false value', () => {
      flushInitialLoad([azure]);

      openEditor('canRepeat', false);
      component.commitEdit();

      const body = flushSave({ ...azureFull, canRepeat: false });

      expect(body['canRepeat']).toBeFalse();
      expect(cell('canRepeat').textContent?.trim()).toBe('否');
    });

    it('skips the request when the value did not change', () => {
      flushInitialLoad([azure]);

      openEditor('title', '  Azure 基礎架構  ');
      component.commitEdit();

      expect(component.editingCell()).toBeNull();
      httpMock.expectNone(azureUrl);
      httpMock.expectNone(coursesUrl);
    });

    it('skips the request when a date is reopened and left alone', () => {
      flushInitialLoad([azure]);

      // startEdit seeds editValue from the row, so committing straight away is a no-op.
      component.startEdit(azure, 'scheduleOn');
      component.commitEdit();

      expect(component.editingCell()).toBeNull();
      httpMock.expectNone(azureUrl);
    });

    describe('validation', () => {
      /** Every case keeps the cell open, shows the message, and sends nothing. */
      function expectBlocked(field: string, value: unknown, message: string): void {
        openEditor(field, value);
        component.commitEdit();
        fixture.detectChanges();

        expect(component.editError()).withContext(field).toBe(message);
        expect(component.editingCell()?.field).withContext(field).toBe(field);
        expect(cell(field).textContent).withContext(field).toContain(message);
        httpMock.expectNone(azureUrl);
        httpMock.expectNone(coursesUrl);

        component.cancelEdit();
        fixture.detectChanges();
      }

      it('blocks a cleared required text column', () => {
        flushInitialLoad([azure]);

        expectBlocked('title', '   ', '此欄位必填，不可清空。');
        expectBlocked('courseId', '', '此欄位必填，不可清空。');
        expectBlocked('prodCourseId', null, '此欄位必填，不可清空。');
      });

      it('blocks a cleared required dropdown, number or date', () => {
        flushInitialLoad([azure]);

        expectBlocked('publishStatusPkid', null, '此欄位必填，不可清空。');
        expectBlocked('hour', null, '此欄位必填，不可清空。');
        expectBlocked('scheduleOn', null, '此欄位必填，不可清空。');
        expectBlocked('scheduleOff', null, '此欄位必填，不可清空。');
      });

      it('blocks a negative 時數, 定價 or 點數', () => {
        flushInitialLoad([azure]);

        expectBlocked('hour', -1, '不可小於 0。');
        expectBlocked('listPrice', -0.5, '不可小於 0。');
        expectBlocked('learningCredit', -30, '不可小於 0。');
        expectBlocked('displayOrder', -20, '不可小於 0。');
      });

      it('blocks a value that is not a number', () => {
        flushInitialLoad([azure]);

        expectBlocked('listPrice', 'NT$24000', '請輸入有效的數字。');
      });

      it('accepts zero — non-negative, not positive', () => {
        flushInitialLoad([azure]);

        openEditor('hour', 0);
        component.commitEdit();

        expect(component.editError()).toBeNull();
        expect(flushSave({ ...azureFull, hour: 0 })['hour']).toBe(0);
      });

      it('blocks a date the picker could not parse', () => {
        flushInitialLoad([azure]);

        expectBlocked('scheduleOn', new Date('not a date'), '請輸入有效的日期。');
        expectBlocked('scheduleOff', 'yesterday', '請輸入有效的日期。');
      });

      it('blocks 上架日期 after 下架日期, from either end', () => {
        flushInitialLoad([azure]);

        // The row runs 2026-01-01 → 2036-01-01.
        expectBlocked('scheduleOn', new Date(2037, 0, 1), '上架日期不可晚於下架日期。');
        expectBlocked('scheduleOff', new Date(2025, 11, 31), '上架日期不可晚於下架日期。');
      });

      it('allows the two dates to meet on the same day', () => {
        flushInitialLoad([azure]);

        openEditor('scheduleOff', new Date(2026, 0, 1));
        component.commitEdit();

        expect(component.editError()).toBeNull();
        expect(flushSave({ ...azureFull, scheduleOff: '2026-01-01' })['scheduleOff']).toBe(
          '2026-01-01',
        );
      });

      it('blocks text longer than the column', () => {
        flushInitialLoad([azure]);

        expectBlocked('courseId', 'A'.repeat(51), '不可超過 50 個字元。');
      });
    });

    it('reverts the cell and reports the error when the save fails', () => {
      flushInitialLoad([azure]);

      const messageService = TestBed.inject(MessageService);
      const add = spyOn(messageService, 'add');

      openEditor('hour', 45);
      component.commitEdit();

      httpMock.expectOne(azureUrl).flush(azureFull);
      httpMock
        .expectOne(coursesUrl)
        .flush('boom', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(component.courses()[0].hour).toBe(30);
      expect(cell('hour').textContent?.trim()).toBe('30');
      expect(component.editingCell()).toBeNull();
      expect(component.savingCell()).toBeFalse();
      expect(add.calls.mostRecent().args[0].summary).toBe('儲存失敗');
      expect(add.calls.mostRecent().args[0].detail).toContain('時數');
    });

    it('reverts when the record cannot be re-read', () => {
      flushInitialLoad([azure]);

      const messageService = TestBed.inject(MessageService);
      const add = spyOn(messageService, 'add');

      openEditor('title', 'Azure 進階架構');
      component.commitEdit();

      httpMock.expectOne(azureUrl).flush('gone', { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(component.courses()[0].title).toBe('Azure 基礎架構');
      expect(component.editingCell()).toBeNull();
      expect(add.calls.mostRecent().args[0].summary).toBe('儲存失敗');
    });

    it('discards the working value on Esc without calling the API', () => {
      flushInitialLoad([azure]);

      openEditor('title', 'thrown away');
      component.cancelEdit();
      fixture.detectChanges();

      expect(component.editingCell()).toBeNull();
      expect(cell('title').textContent?.trim()).toBe('Azure 基礎架構');
      httpMock.expectNone(azureUrl);
    });
  });
});
