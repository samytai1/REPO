import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { Course, CourseRequest, CourseUpdateRequest } from '@core/models';
import { CourseService } from './course.service';

describe('CourseService', () => {
  const baseUrl = `${environment.apiBaseUrl}/courses`;

  let service: CourseService;
  let httpMock: HttpTestingController;

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
    jobCategories: [{ pkid: 5, description: '系統管理' }],
    certifications: [{ pkid: 100, title: 'AZ-104', partnerPkid: 1 }],
  };

  const request: CourseRequest = {
    title: 'Linux 系統管理',
    officialTitle: null,
    courseId: 'LX-101',
    prodCourseId: 'UU-LX101',
    friendlyUrl: 'linux-admin',
    displayOrder: 40,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 2,
    scheduleOn: '2026-09-01',
    scheduleOff: '2036-09-01',
    hour: 24,
    listPrice: 18000,
    learningCredit: 24,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: false,
    jobCategoryPkids: [5],
    certificationPkids: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CourseService],
    });

    service = TestBed.inject(CourseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET /api/courses', () => {
    let result: Course[] | undefined;
    service.getAll().subscribe((courses) => (result = courses));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([azure]);

    expect(result).toEqual([azure]);
  });

  it('query() POSTs the filter to the /query sub-route', () => {
    const filter = {
      keyword: 'Azure',
      partnerPkid: 1,
      courseGroupPkid: null,
      publishStatusPkid: 2,
      canRepeat: true,
      scheduleOnFrom: '2026-01-01',
      scheduleOnTo: null,
      scheduleOffFrom: null,
      scheduleOffTo: '2040-12-31',
    };

    let result: Course[] | undefined;
    service.query(filter).subscribe((courses) => (result = courses));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([azure]);

    expect(result?.length).toBe(1);
  });

  it('getById() issues GET with the numeric key in the path', () => {
    let result: Course | undefined;
    service.getById(1).subscribe((course) => (result = course));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(azure);

    expect(result?.jobCategories.length).toBe(1);
  });

  it('create() POSTs a body without a pkid, because the column is IDENTITY', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.body.pkid).toBeUndefined();
    req.flush({ ...azure, pkid: 4, title: request.title });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const update: CourseUpdateRequest = { ...request, pkid: 1 };

    service.update(update).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(baseUrl);
    expect(req.request.body.pkid).toBe(1);
    expect(req.request.body.jobCategoryPkids).toEqual([5]);
    req.flush(azure);
  });

  it('delete() issues DELETE with the key in the path', () => {
    service.delete(3).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('surfaces a 409 from a still-referenced row to the caller', () => {
    let status: number | undefined;
    service.delete(2).subscribe({ error: (err) => (status = err.status) });

    httpMock.expectOne(`${baseUrl}/2`).flush('Conflict', {
      status: 409,
      statusText: 'Conflict',
    });

    expect(status).toBe(409);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;
    service.getById(99).subscribe({ error: (err) => (status = err.status) });

    httpMock.expectOne(`${baseUrl}/99`).flush('Not Found', {
      status: 404,
      statusText: 'Not Found',
    });

    expect(status).toBe(404);
  });
});
