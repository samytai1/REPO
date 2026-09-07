import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { environment } from '@environments/environment';

import { AppRole } from '@core/models';
import { AppRoleList } from './app-role-list';

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  roles: () => AppRole[];
  loading: () => boolean;
  drawerVisible: () => boolean;
  filters: { keyword?: string | null; permissionLevelFrom?: number | null; permissionLevelTo?: number | null };
  draftFilters: {
    keyword?: string | null;
    permissionLevelFrom?: number | null;
    permissionLevelTo?: number | null;
  };
  sort: { field: string; order: number };
  page: { first: number; rows: number };
  hasActiveFilters: boolean;
  openDrawer(): void;
  applyFilters(): void;
  resetFilters(): void;
  onStateChange(event: { first?: number; rows?: number; sortField?: string; sortOrder?: number }): void;
  view(role: AppRole): void;
  edit(role: AppRole): void;
  add(): void;
  confirmDelete(role: AppRole): void;
};

describe('AppRoleList', () => {
  const queryUrl = `${environment.apiBaseUrl}/app-roles/query`;

  const roles: AppRole[] = [
    {
      pkid: 1,
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: '系統管理員',
      userCount: 3,
      users: [],
    },
    {
      pkid: 2,
      roleId: 'User',
      roleName: 'User',
      permissionLevel: 100,
      description: '一般使用者',
      userCount: 9,
      users: [],
    },
  ];

  let fixture: ComponentFixture<AppRoleList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Answers the initial POST /query that ngOnInit fires. */
  function flushInitialLoad(payload: AppRole[] = roles): void {
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates and loads roles through POST /api/app-roles/query', () => {
    flushInitialLoad();

    expect(component.roles().length).toBe(2);
    expect(component.loading()).toBeFalse();
  });

  it('renders a row per role', () => {
    flushInitialLoad();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Administrator');
    expect(fixture.nativeElement.textContent).toContain('系統管理員');
  });

  it('shows the user count column', () => {
    flushInitialLoad();

    const firstRowCells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(firstRowCells[5].textContent.trim()).toBe('3');
  });

  it('shows the empty message when the API returns nothing', () => {
    flushInitialLoad([]);

    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('clears the list and stops loading when the request fails', () => {
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.roles()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  it('sends the drawer filters on the next query', () => {
    flushInitialLoad();

    component.openDrawer();
    component.draftFilters.keyword = 'adm';
    component.draftFilters.permissionLevelFrom = 1;
    component.draftFilters.permissionLevelTo = 50;
    component.applyFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({ keyword: 'adm', permissionLevelFrom: 1, permissionLevelTo: 50 }),
    );
    req.flush([roles[0]]);

    expect(component.roles().length).toBe(1);
    expect(component.drawerVisible()).toBeFalse();
    expect(component.hasActiveFilters).toBeTrue();
  });

  it('persists the applied filters to session storage', () => {
    flushInitialLoad();

    component.draftFilters.keyword = 'user';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([roles[1]]);

    const stored = JSON.parse(sessionStorage.getItem('app-role-list-filters') ?? '{}');
    expect(stored.keyword).toBe('user');
  });

  it('restores persisted filters on init', () => {
    httpMock.expectOne(queryUrl).flush(roles);

    sessionStorage.setItem('app-role-list-filters', JSON.stringify({ keyword: 'restored' }));
    sessionStorage.setItem('app-role-list-sort', JSON.stringify({ field: 'roleName', order: -1 }));
    sessionStorage.setItem('app-role-list-page', JSON.stringify({ first: 0, rows: 50 }));

    const restored = TestBed.createComponent(AppRoleList);
    const restoredComponent = restored.componentInstance as unknown as ListInternals;
    restored.detectChanges();

    expect(restoredComponent.filters.keyword).toBe('restored');
    expect(restoredComponent.sort).toEqual({ field: 'roleName', order: -1 });
    expect(restoredComponent.page).toEqual({ first: 0, rows: 50 });

    httpMock.expectOne(queryUrl).flush(roles);
  });

  it('resets the filters and reloads', () => {
    flushInitialLoad();

    component.draftFilters.keyword = 'adm';
    component.applyFilters();
    httpMock.expectOne(queryUrl).flush([roles[0]]);

    component.resetFilters();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(
      jasmine.objectContaining({ keyword: null, permissionLevelFrom: null, permissionLevelTo: null }),
    );
    req.flush(roles);

    expect(component.hasActiveFilters).toBeFalse();
    expect(sessionStorage.getItem('app-role-list-filters')).toBeNull();
  });

  it('persists sort and page changes', () => {
    flushInitialLoad();

    component.onStateChange({ first: 40, rows: 20, sortField: 'permissionLevel', sortOrder: -1 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort') ?? '{}')).toEqual({
      field: 'permissionLevel',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('app-role-list-page') ?? '{}')).toEqual({
      first: 40,
      rows: 20,
    });
  });

  it('navigates to the detail, edit and add pages', () => {
    flushInitialLoad();
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    component.view(roles[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin']);

    component.edit(roles[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin', 'edit']);

    component.add();
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'new']);
  });

  it('deletes only after the confirmation is accepted, then reloads', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    component.confirmDelete(roles[1]);

    const deleteReq = httpMock.expectOne(`${environment.apiBaseUrl}/app-roles/User`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([roles[0]]);
    expect(component.roles().length).toBe(1);
  });

  it('does not delete when the confirmation is dismissed', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(roles[1]);

    httpMock.expectNone(`${environment.apiBaseUrl}/app-roles/User`);
    expect(component.roles().length).toBe(2);
  });
});
