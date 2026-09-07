import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AppRole, AppUserLookup } from '@core/models';
import { AppRoleForm } from './app-role-form';

type FormInternals = {
  form: FormGroup;
  loading: () => boolean;
  saving: () => boolean;
  users: () => AppUserLookup[];
  isEditMode: boolean;
  userOptions: { label: string; value: string }[];
  save(): void;
  cancel(): void;
};

describe('AppRoleForm', () => {
  const usersUrl = `${environment.apiBaseUrl}/lookups/app-users`;
  const rolesUrl = `${environment.apiBaseUrl}/app-roles`;

  const users: AppUserLookup[] = [
    { userId: 'helen', userName: 'helen', isActive: true },
    { userId: 'Jenny_Tsao', userName: 'Jenny_Tsao', isActive: true },
    { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true },
  ];

  const adminRole: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    users: [users[0], users[2]],
  };

  let fixture: ComponentFixture<AppRoleForm>;
  let component: FormInternals;
  let httpMock: HttpTestingController;

  /** `roleId` null => add mode; a value => edit mode. */
  async function setup(roleId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppRoleForm],
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

    fixture = TestBed.createComponent(AppRoleForm);
    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();

    httpMock.expectOne(usersUrl).flush(users);
    if (roleId) {
      httpMock.expectOne(`${rolesUrl}/${encodeURIComponent(roleId)}`).flush(adminRole);
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  it('starts empty in add mode, with the schema default permission level', async () => {
    await setup(null);

    expect(component.isEditMode).toBeFalse();
    expect(component.form.getRawValue()).toEqual({
      roleId: '',
      roleName: '',
      permissionLevel: 100,
      description: '',
      userIds: [],
    });
  });

  it('loads the user lookup in parallel and maps it to options', async () => {
    await setup(null);

    expect(component.users().length).toBe(3);
    expect(component.userOptions).toContain({ label: 'Miles Sun (miles@uuu.com.tw)', value: 'miles@uuu.com.tw' });
  });

  it('shows the add-mode heading and leaves the key editable', async () => {
    await setup(null);

    expect(fixture.nativeElement.textContent).toContain('新增角色');
    expect(component.form.controls['roleId'].disabled).toBeFalse();
  });

  it('does not submit while required fields are missing', async () => {
    await setup(null);

    component.save();

    httpMock.expectNone(rolesUrl);
    expect(component.form.controls['roleName'].touched).toBeTrue();
  });

  it('POSTs a trimmed request on save in add mode', async () => {
    await setup(null);

    component.form.patchValue({
      roleId: '  Editor  ',
      roleName: '  Content Editor  ',
      permissionLevel: 50,
      description: '  內容編輯  ',
      userIds: ['helen'],
    });

    component.save();

    const req = httpMock.expectOne(rolesUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      roleId: 'Editor',
      roleName: 'Content Editor',
      permissionLevel: 50,
      description: '內容編輯',
      userIds: ['helen'],
    });

    req.flush({ ...adminRole, roleId: 'Editor', roleName: 'Content Editor' });
    expect(component.saving()).toBeFalse();
  });

  it('sends a null description when the field is left blank', async () => {
    await setup(null);

    component.form.patchValue({ roleId: 'Editor', roleName: 'Content Editor', description: '   ' });
    component.save();

    const req = httpMock.expectOne(rolesUrl);
    expect(req.request.body.description).toBeNull();
    req.flush(adminRole);
  });

  it('navigates to the saved record after a successful create', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.form.patchValue({ roleId: 'Editor', roleName: 'Content Editor' });
    component.save();
    httpMock.expectOne(rolesUrl).flush({ ...adminRole, roleId: 'Editor' });

    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Editor']);
  });

  it('stops the saving state when the API rejects a duplicate key', async () => {
    await setup(null);

    component.form.patchValue({ roleId: 'Admin', roleName: 'Duplicate' });
    component.save();

    httpMock.expectOne(rolesUrl).flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(component.saving()).toBeFalse();
  });

  // ---------- Edit mode ----------

  it('loads the record and its user assignments in edit mode', async () => {
    await setup('Admin');

    expect(component.isEditMode).toBeTrue();
    expect(component.form.getRawValue()).toEqual({
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: '系統管理員',
      userIds: ['helen', 'miles@uuu.com.tw'],
    });
  });

  it('locks the key field in edit mode', async () => {
    await setup('Admin');

    expect(component.form.controls['roleId'].disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('編輯角色');
  });

  it('PUTs to the collection route with the key still in the body', async () => {
    await setup('Admin');

    component.form.patchValue({ roleName: 'Administrators', permissionLevel: 2, userIds: ['helen'] });
    component.save();

    const req = httpMock.expectOne(rolesUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      roleId: 'Admin',
      roleName: 'Administrators',
      permissionLevel: 2,
      description: '系統管理員',
      userIds: ['helen'],
    });

    req.flush({ ...adminRole, roleName: 'Administrators' });
  });

  it('can clear every user assignment', async () => {
    await setup('Admin');

    component.form.patchValue({ userIds: [] });
    component.save();

    const req = httpMock.expectOne(rolesUrl);
    expect(req.request.body.userIds).toEqual([]);
    req.flush(adminRole);
  });

  it('converts a null description from the API into an empty control value', async () => {
    await TestBed.configureTestingModule({
      imports: [AppRoleForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'User' } } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleForm);
    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    httpMock.expectOne(usersUrl).flush(users);
    httpMock
      .expectOne(`${rolesUrl}/User`)
      .flush({ ...adminRole, roleId: 'User', description: null, users: [] });
    fixture.detectChanges();

    expect(component.form.controls['description'].value).toBe('');
  });

  it('cancel returns to the record in edit mode and to the list in add mode', async () => {
    await setup('Admin');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin']);
  });

  it('cancel returns to the list in add mode', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/app-roles']);
  });
});
