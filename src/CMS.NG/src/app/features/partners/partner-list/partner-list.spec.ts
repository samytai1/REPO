import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { environment } from '@environments/environment';

import { Partner, PartnerQuery } from '@core/models';
import { PartnerList } from './partner-list';

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  partners: () => Partner[];
  loading: () => boolean;
  drawerVisible: () => boolean;
  filters: PartnerQuery;
  draftFilters: PartnerQuery;
  sort: { field: string; order: number };
  page: { first: number; rows: number };
  hasActiveFilters: boolean;
  openDrawer(): void;
  applyFilters(): void;
  resetFilters(): void;
  onStateChange(event: {
    first?: number;
    rows?: number;
    sortField?: string;
    sortOrder?: number;
  }): void;
  view(partner: Partner): void;
  edit(partner: Partner): void;
  add(): void;
  confirmDelete(partner: Partner): void;
};

describe('PartnerList', () => {
  const queryUrl = `${environment.apiBaseUrl}/partners/query`;

  const partners: Partner[] = [
    {
      pkid: 2,
      name: '思科',
      appKey: 'CISCO',
      nameOnPartnerMenu: 'Cisco 思科課程',
      nameOnCourseDetailPage: '思科',
      displayOrder: 10,
      imageFilename: 'cisco.png',
    },
    {
      pkid: 3,
      name: '自辦課程',
      appKey: 'OWN',
      nameOnPartnerMenu: '恆逸自辦課程',
      nameOnCourseDetailPage: '恆逸',
      displayOrder: 30,
      imageFilename: null,
    },
  ];

  let fixture: ComponentFixture<PartnerList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Answers the initial POST /query that ngOnInit fires. */
  function flushInitialLoad(payload: Partner[] = partners): void {
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates and loads partners through POST /api/partners/query', () => {
    flushInitialLoad();

    expect(component.partners().length).toBe(2);
    expect(component.loading()).toBeFalse();
  });

  it('renders a row per partner', () => {
    flushInitialLoad();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('思科');
    expect(fixture.nativeElement.textContent).toContain('自辦課程');
  });

  it('renders every column of a row', () => {
    flushInitialLoad();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(cells[0].textContent.trim()).toBe('2');
    expect(cells[1].textContent.trim()).toBe('思科');
    expect(cells[2].textContent.trim()).toBe('CISCO');
    expect(cells[3].textContent.trim()).toBe('Cisco 思科課程');
    expect(cells[4].textContent.trim()).toBe('思科');
    expect(cells[5].textContent.trim()).toBe('10');
    expect(cells[6].textContent.trim()).toBe('cisco.png');
  });

  it('falls back to a dash when a partner has no image filename', () => {
    flushInitialLoad();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr:last-child td');
    expect(cells[6].textContent.trim()).toBe('—');
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
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.partners()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  it('sends the drawer filters on the next query', () => {
    flushInitialLoad();

    component.openDrawer();
    component.draftFilters.keyword = '思';
    component.draftFilters.displayOrderFrom = 1;
    component.draftFilters.displayOrderTo = 20;
    component.draftFilters.hasImage = true;
    component.applyFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({
        keyword: '思',
        displayOrderFrom: 1,
        displayOrderTo: 20,
        hasImage: true,
      }),
    );
    req.flush([partners[0]]);

    expect(component.partners().length).toBe(1);
    expect(component.drawerVisible()).toBeFalse();
    expect(component.hasActiveFilters).toBeTrue();
  });

  it('treats a false hasImage filter as active, not as "unset"', () => {
    flushInitialLoad();

    component.draftFilters.hasImage = false;
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([partners[1]]);

    expect(component.hasActiveFilters).toBeTrue();
  });

  it('persists the applied filters to session storage', () => {
    flushInitialLoad();

    component.draftFilters.keyword = '思科';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([partners[0]]);

    const stored = JSON.parse(sessionStorage.getItem('partner-list-filters') ?? '{}');
    expect(stored.keyword).toBe('思科');
  });

  it('restores persisted filters on init', () => {
    httpMock.expectOne(queryUrl).flush(partners);

    sessionStorage.setItem(
      'partner-list-filters',
      JSON.stringify({ keyword: 'restored', hasImage: false }),
    );
    sessionStorage.setItem('partner-list-sort', JSON.stringify({ field: 'name', order: -1 }));
    sessionStorage.setItem('partner-list-page', JSON.stringify({ first: 0, rows: 50 }));

    const restored = TestBed.createComponent(PartnerList);
    const restoredComponent = restored.componentInstance as unknown as ListInternals;
    restored.detectChanges();

    expect(restoredComponent.filters.keyword).toBe('restored');
    expect(restoredComponent.filters.hasImage).toBeFalse();
    expect(restoredComponent.sort).toEqual({ field: 'name', order: -1 });
    expect(restoredComponent.page).toEqual({ first: 0, rows: 50 });

    httpMock.expectOne(queryUrl).flush(partners);
  });

  it('resets the filters and reloads', () => {
    flushInitialLoad();

    component.draftFilters.keyword = '思';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([partners[0]]);

    component.resetFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({
        keyword: null,
        displayOrderFrom: null,
        displayOrderTo: null,
        hasImage: null,
      }),
    );
    req.flush(partners);

    expect(component.hasActiveFilters).toBeFalse();
    expect(sessionStorage.getItem('partner-list-filters')).toBeNull();
  });

  it('persists sort and page changes', () => {
    flushInitialLoad();

    component.onStateChange({ first: 20, rows: 20, sortField: 'name', sortOrder: -1 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-sort') ?? '{}')).toEqual({
      field: 'name',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('partner-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('navigates to the detail, edit and add pages', () => {
    flushInitialLoad();
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    component.view(partners[0]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 2]);

    component.edit(partners[0]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 2, 'edit']);

    component.add();
    expect(navigate).toHaveBeenCalledWith(['/partners', 'new']);
  });

  it('deletes only after the confirmation is accepted, then reloads', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    component.confirmDelete(partners[1]);

    const deleteReq = httpMock.expectOne(`${environment.apiBaseUrl}/partners/3`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([partners[0]]);
    expect(component.partners().length).toBe(1);
  });

  it('names the record in the delete confirmation', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(partners[0]);

    expect(confirm.calls.mostRecent().args[0].message).toContain('主代碼 2「思科」');
  });

  it('reports a 409 delete as a still-referenced partner', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    const add = spyOn(messageService, 'add');

    component.confirmDelete(partners[0]);

    httpMock
      .expectOne(`${environment.apiBaseUrl}/partners/2`)
      .flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(add.calls.mostRecent().args[0].detail).toContain('已被認證、課程或課程群組使用');
    expect(component.partners().length).toBe(2);
  });

  it('does not delete when the confirmation is dismissed', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(partners[0]);

    httpMock.expectNone(`${environment.apiBaseUrl}/partners/2`);
    expect(component.partners().length).toBe(2);
  });
});
