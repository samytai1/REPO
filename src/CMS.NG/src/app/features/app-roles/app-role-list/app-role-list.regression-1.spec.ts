import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AppRole } from '@core/models';
import { AppRoleList } from './app-role-list';

// Regression: ISSUE-001 — a header click never wrote `app-role-list-sort` and reset the stored
//   page to 0, because `onStateChange` read `sortField`/`first` off an `(onSort)` event that
//   carries `{ field, order }`. Driven through the real header rather than a hand-built event.
// Found by /qa on 2026-09-10
// Report: .gstack/qa-reports/qa-report-localhost-4200-2026-09-10.md

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  sort: { field: string; order: number };
  page: { first: number; rows: number };
};

describe('AppRoleList (regression ISSUE-001: sort persistence from the real (onSort) event)', () => {
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

  function flushInitialLoad(): void {
    httpMock.expectOne(queryUrl).flush(roles);
    fixture.detectChanges();
  }

  function clickHeader(field: string): void {
    const header = fixture.nativeElement.querySelector(
      `th[psortablecolumn="${field}"]`,
    ) as HTMLElement | null;
    expect(header).withContext(`a sortable ${field} header`).not.toBeNull();
    header!.click();
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

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('writes app-role-list-sort when a column header is clicked, and keeps the rows-per-page', () => {
    flushInitialLoad();
    sessionStorage.setItem('app-role-list-page', JSON.stringify({ first: 0, rows: 50 }));
    component.page = { first: 0, rows: 50 };

    clickHeader('roleName');

    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort') ?? 'null')).toEqual({
      field: 'roleName',
      order: 1,
    });
    expect(component.sort).toEqual({ field: 'roleName', order: 1 });
    // The sort event carries no `rows`; the setting must not fall back to the default.
    expect(JSON.parse(sessionStorage.getItem('app-role-list-page') ?? 'null')).toEqual({
      first: 0,
      rows: 50,
    });
  });
});
