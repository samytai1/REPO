import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AppRole } from '@core/models';
import { AppRoleDetail } from './app-role-detail';

type DetailInternals = {
  role: () => AppRole | null;
  loading: () => boolean;
  back(): void;
  edit(): void;
};

describe('AppRoleDetail', () => {
  const adminRole: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    users: [
      { userId: 'helen', userName: 'helen', isActive: true },
      { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: false },
    ],
  };

  let fixture: ComponentFixture<AppRoleDetail>;
  let component: DetailInternals;
  let httpMock: HttpTestingController;

  async function setup(roleId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => roleId } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleDetail);
    component = fixture.componentInstance as unknown as DetailInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('loads the role named in the route', async () => {
    await setup('Admin');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/app-roles/Admin`);
    expect(req.request.method).toBe('GET');
    req.flush(adminRole);
    fixture.detectChanges();

    expect(component.role()?.roleName).toBe('Administrator');
    expect(component.loading()).toBeFalse();
  });

  it('renders every field of the role', async () => {
    await setup('Admin');
    httpMock.expectOne(`${environment.apiBaseUrl}/app-roles/Admin`).flush(adminRole);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
    expect(text).toContain('1');
  });

  it('lists the assigned users', async () => {
    await setup('Admin');
    httpMock.expectOne(`${environment.apiBaseUrl}/app-roles/Admin`).flush(adminRole);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('helen (helen)');
    expect(text).toContain('Miles Sun (miles@uuu.com.tw)');
  });

  it('shows a placeholder when the role has no users', async () => {
    await setup('User');
    httpMock
      .expectOne(`${environment.apiBaseUrl}/app-roles/User`)
      .flush({ ...adminRole, roleId: 'User', userCount: 0, users: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('尚無使用者');
  });

  it('URL-encodes a key containing unsafe characters', async () => {
    await setup('Ops/Support');

    const req = httpMock.expectOne(
      `${environment.apiBaseUrl}/app-roles/${encodeURIComponent('Ops/Support')}`,
    );
    req.flush({ ...adminRole, roleId: 'Ops/Support' });
    fixture.detectChanges();

    expect(component.role()?.roleId).toBe('Ops/Support');
  });

  it('falls back to the empty state when the role is not found', async () => {
    await setup('Ghost');

    httpMock
      .expectOne(`${environment.apiBaseUrl}/app-roles/Ghost`)
      .flush('Not Found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(component.role()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('navigates back to the list and into the edit form', async () => {
    await setup('Admin');
    httpMock.expectOne(`${environment.apiBaseUrl}/app-roles/Admin`).flush(adminRole);
    fixture.detectChanges();

    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.back();
    expect(navigate).toHaveBeenCalledWith(['/app-roles']);

    component.edit();
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin', 'edit']);
  });
});
