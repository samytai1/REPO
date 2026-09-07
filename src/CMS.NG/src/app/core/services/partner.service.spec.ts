import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { Partner, PartnerRequest, PartnerUpdateRequest } from '@core/models';
import { PartnerService } from './partner.service';

describe('PartnerService', () => {
  const baseUrl = `${environment.apiBaseUrl}/partners`;

  let service: PartnerService;
  let httpMock: HttpTestingController;

  const microsoft: Partner = {
    pkid: 1,
    name: '微軟',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟課程',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 20,
    imageFilename: 'ms-logo.png',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PartnerService],
    });

    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET /api/partners', () => {
    let result: Partner[] | undefined;
    service.getAll().subscribe((partners) => (result = partners));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([microsoft]);

    expect(result).toEqual([microsoft]);
  });

  it('query() POSTs the filter to the /query sub-route', () => {
    const filter = {
      keyword: '微',
      displayOrderFrom: 1,
      displayOrderTo: 99,
      hasImage: true,
    };

    let result: Partner[] | undefined;
    service.query(filter).subscribe((partners) => (result = partners));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([microsoft]);

    expect(result?.length).toBe(1);
  });

  it('getById() issues GET with the numeric key in the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(microsoft);
  });

  it('create() POSTs a body without a pkid, because the column is IDENTITY', () => {
    const request: PartnerRequest = {
      name: '紅帽',
      appKey: 'RH',
      nameOnPartnerMenu: 'Red Hat 紅帽課程',
      nameOnCourseDetailPage: '紅帽',
      displayOrder: 40,
      imageFilename: null,
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.body.pkid).toBeUndefined();
    req.flush({ ...microsoft, pkid: 4, name: '紅帽' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: PartnerUpdateRequest = {
      pkid: 1,
      name: '微軟',
      appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 微軟課程',
      nameOnCourseDetailPage: '微軟',
      displayOrder: 20,
      imageFilename: 'ms-logo.png',
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(baseUrl);
    expect(req.request.body.pkid).toBe(1);
    req.flush(microsoft);
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
