import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { PublishStatus, PublishStatusRequest } from '@core/models';
import { PublishStatusService } from './publish-status.service';

describe('PublishStatusService', () => {
  const baseUrl = `${environment.apiBaseUrl}/publish-statuses`;

  let service: PublishStatusService;
  let httpMock: HttpTestingController;

  const draft: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PublishStatusService],
    });

    service = TestBed.inject(PublishStatusService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET /api/publish-statuses', () => {
    let result: PublishStatus[] | undefined;
    service.getAll().subscribe((statuses) => (result = statuses));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([draft]);

    expect(result).toEqual([draft]);
  });

  it('query() POSTs the filter to the /query sub-route', () => {
    const filter = {
      keyword: '草',
      pkidFrom: 1,
      pkidTo: 9,
      isDraft: true,
      isPublished: null,
      isDiscontinued: false,
    };

    let result: PublishStatus[] | undefined;
    service.query(filter).subscribe((statuses) => (result = statuses));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([draft]);

    expect(result?.length).toBe(1);
  });

  it('getById() issues GET with the numeric key in the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(draft);
  });

  it('create() POSTs the request body to the collection route', () => {
    const request: PublishStatusRequest = {
      pkid: 4,
      description: '審核中',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...draft, pkid: 4, description: '審核中' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: PublishStatusRequest = {
      pkid: 1,
      description: '草稿',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(baseUrl);
    expect(req.request.body.pkid).toBe(1);
    req.flush(draft);
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
