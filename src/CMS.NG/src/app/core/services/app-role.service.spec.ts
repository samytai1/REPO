import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { AppRole, AppRoleRequest } from '@core/models';
import { AppRoleService } from './app-role.service';

describe('AppRoleService', () => {
  const baseUrl = `${environment.apiBaseUrl}/app-roles`;

  let service: AppRoleService;
  let httpMock: HttpTestingController;

  const adminRole: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    users: [
      { userId: 'helen', userName: 'helen', isActive: true },
      { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), AppRoleService],
    });

    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET /api/app-roles', () => {
    let result: AppRole[] | undefined;
    service.getAll().subscribe((roles) => (result = roles));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([adminRole]);

    expect(result).toEqual([adminRole]);
  });

  it('query() POSTs the filter to the /query sub-route', () => {
    const filter = { keyword: 'adm', permissionLevelFrom: 1, permissionLevelTo: 50 };

    let result: AppRole[] | undefined;
    service.query(filter).subscribe((roles) => (result = roles));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([adminRole]);

    expect(result?.length).toBe(1);
  });

  it('getById() issues GET with the id in the path', () => {
    service.getById('Admin').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/Admin`);
    expect(req.request.method).toBe('GET');
    req.flush(adminRole);
  });

  it('getById() URL-encodes keys containing unsafe characters', () => {
    service.getById('Ops/Support 群組').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/${encodeURIComponent('Ops/Support 群組')}`);
    expect(req.request.url).toContain('Ops%2FSupport');
    req.flush(adminRole);
  });

  it('create() POSTs the request body to the collection route', () => {
    const request: AppRoleRequest = {
      roleId: 'Editor',
      roleName: 'Content Editor',
      permissionLevel: 50,
      description: '內容編輯',
      userIds: ['helen'],
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...adminRole, roleId: 'Editor' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: AppRoleRequest = {
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: null,
      userIds: [],
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(baseUrl);
    expect(req.request.body.roleId).toBe('Admin');
    req.flush(adminRole);
  });

  it('delete() issues DELETE with the encoded id', () => {
    service.delete('Ops/Support').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/${encodeURIComponent('Ops/Support')}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;
    service.getById('Ghost').subscribe({ error: (err) => (status = err.status) });

    httpMock.expectOne(`${baseUrl}/Ghost`).flush('Not Found', {
      status: 404,
      statusText: 'Not Found',
    });

    expect(status).toBe(404);
  });
});
