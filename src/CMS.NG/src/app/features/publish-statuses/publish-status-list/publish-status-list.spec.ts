import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { environment } from '@environments/environment';

import { PublishStatus, PublishStatusQuery } from '@core/models';
import { PublishStatusList } from './publish-status-list';

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  statuses: () => PublishStatus[];
  loading: () => boolean;
  drawerVisible: () => boolean;
  filters: PublishStatusQuery;
  draftFilters: PublishStatusQuery;
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
  view(status: PublishStatus): void;
  edit(status: PublishStatus): void;
  add(): void;
  confirmDelete(status: PublishStatus): void;
};

describe('PublishStatusList', () => {
  const queryUrl = `${environment.apiBaseUrl}/publish-statuses/query`;

  const statuses: PublishStatus[] = [
    { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
    { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
  ];

  let fixture: ComponentFixture<PublishStatusList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Answers the initial POST /query that ngOnInit fires. */
  function flushInitialLoad(payload: PublishStatus[] = statuses): void {
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates and loads statuses through POST /api/publish-statuses/query', () => {
    flushInitialLoad();

    expect(component.statuses().length).toBe(2);
    expect(component.loading()).toBeFalse();
  });

  it('renders a row per status', () => {
    flushInitialLoad();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('草稿');
    expect(fixture.nativeElement.textContent).toContain('已發布');
  });

  it('renders the bit columns as 是／否 tags rather than raw booleans', () => {
    flushInitialLoad();

    const firstRowCells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(firstRowCells[2].textContent.trim()).toBe('是');
    expect(firstRowCells[3].textContent.trim()).toBe('否');
    expect(firstRowCells[4].textContent.trim()).toBe('否');
  });

  it('shows the empty message when the API returns nothing', () => {
    flushInitialLoad([]);

    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('clears the list and stops loading when the request fails', () => {
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.statuses()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  it('sends the drawer filters on the next query', () => {
    flushInitialLoad();

    component.openDrawer();
    component.draftFilters.keyword = '草';
    component.draftFilters.pkidFrom = 1;
    component.draftFilters.pkidTo = 9;
    component.draftFilters.isDraft = true;
    component.applyFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({ keyword: '草', pkidFrom: 1, pkidTo: 9, isDraft: true }),
    );
    req.flush([statuses[0]]);

    expect(component.statuses().length).toBe(1);
    expect(component.drawerVisible()).toBeFalse();
    expect(component.hasActiveFilters).toBeTrue();
  });

  it('treats a false bool filter as active, not as "unset"', () => {
    flushInitialLoad();

    component.draftFilters.isPublished = false;
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([statuses[0]]);

    expect(component.hasActiveFilters).toBeTrue();
  });

  it('persists the applied filters to session storage', () => {
    flushInitialLoad();

    component.draftFilters.keyword = '已發布';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([statuses[1]]);

    const stored = JSON.parse(sessionStorage.getItem('publish-status-list-filters') ?? '{}');
    expect(stored.keyword).toBe('已發布');
  });

  it('restores persisted filters on init', () => {
    httpMock.expectOne(queryUrl).flush(statuses);

    sessionStorage.setItem(
      'publish-status-list-filters',
      JSON.stringify({ keyword: 'restored', isDiscontinued: true }),
    );
    sessionStorage.setItem(
      'publish-status-list-sort',
      JSON.stringify({ field: 'description', order: -1 }),
    );
    sessionStorage.setItem('publish-status-list-page', JSON.stringify({ first: 0, rows: 50 }));

    const restored = TestBed.createComponent(PublishStatusList);
    const restoredComponent = restored.componentInstance as unknown as ListInternals;
    restored.detectChanges();

    expect(restoredComponent.filters.keyword).toBe('restored');
    expect(restoredComponent.filters.isDiscontinued).toBeTrue();
    expect(restoredComponent.sort).toEqual({ field: 'description', order: -1 });
    expect(restoredComponent.page).toEqual({ first: 0, rows: 50 });

    httpMock.expectOne(queryUrl).flush(statuses);
  });

  it('resets the filters and reloads', () => {
    flushInitialLoad();

    component.draftFilters.keyword = '草';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([statuses[0]]);

    component.resetFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({
        keyword: null,
        pkidFrom: null,
        pkidTo: null,
        isDraft: null,
        isPublished: null,
        isDiscontinued: null,
      }),
    );
    req.flush(statuses);

    expect(component.hasActiveFilters).toBeFalse();
    expect(sessionStorage.getItem('publish-status-list-filters')).toBeNull();
  });

  it('persists sort and page changes', () => {
    flushInitialLoad();

    component.onStateChange({ first: 20, rows: 20, sortField: 'description', sortOrder: -1 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort') ?? '{}')).toEqual({
      field: 'description',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page') ?? '{}')).toEqual({
      first: 20,
      rows: 20,
    });
  });

  it('navigates to the detail, edit and add pages', () => {
    flushInitialLoad();
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    component.view(statuses[0]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1]);

    component.edit(statuses[0]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1, 'edit']);

    component.add();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 'new']);
  });

  it('deletes only after the confirmation is accepted, then reloads', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    component.confirmDelete(statuses[1]);

    const deleteReq = httpMock.expectOne(`${environment.apiBaseUrl}/publish-statuses/2`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([statuses[0]]);
    expect(component.statuses().length).toBe(1);
  });

  it('names the record in the delete confirmation', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(statuses[1]);

    expect(confirm.calls.mostRecent().args[0].message).toContain('主代碼 2「已發布」');
  });

  it('reports a 409 delete as a still-referenced status', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    const add = spyOn(messageService, 'add');

    component.confirmDelete(statuses[1]);

    httpMock
      .expectOne(`${environment.apiBaseUrl}/publish-statuses/2`)
      .flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(add.calls.mostRecent().args[0].detail).toContain('已被課程或促銷使用');
    expect(component.statuses().length).toBe(2);
  });

  it('does not delete when the confirmation is dismissed', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(statuses[1]);

    httpMock.expectNone(`${environment.apiBaseUrl}/publish-statuses/2`);
    expect(component.statuses().length).toBe(2);
  });
});
