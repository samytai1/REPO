import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { FeaturedPromoItem, FeaturedPromoItemRequest, FeaturedPromoItemUpdateRequest } from '@core/models';
import { FeaturedPromoItemService } from './featured-promo-item.service';

describe('FeaturedPromoItemService', () => {
  const baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;

  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;

  const monday1: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    promotion: { pkid: 10, promoCode: '20251204_SkillTrainAI' },
    trainingCenter: { pkid: 1, name: '台北' },
  };

  const request: FeaturedPromoItemRequest = {
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 3,
    promotionPkid: 12,
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), FeaturedPromoItemService],
    });

    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET /api/featured-promo-items', () => {
    let result: FeaturedPromoItem[] | undefined;
    service.getAll().subscribe((items) => (result = items));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([monday1]);

    expect(result).toEqual([monday1]);
  });

  it('query() POSTs the training center and week to the /query sub-route', () => {
    const filter = { trainingCenterPkid: 1, weekOf: '2026-03-16' };

    let result: FeaturedPromoItem[] | undefined;
    service.query(filter).subscribe((items) => (result = items));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([monday1]);

    expect(result?.length).toBe(1);
  });

  it('getById() issues GET with the numeric key in the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(monday1);
  });

  it('create() POSTs a body without a pkid, because the column is IDENTITY', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.body.pkid).toBeUndefined();
    req.flush({ ...monday1, pkid: 8, slot: 3 });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const update: FeaturedPromoItemUpdateRequest = { ...request, pkid: 1 };

    service.update(update).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(baseUrl);
    expect(req.request.body.pkid).toBe(1);
    req.flush(monday1);
  });

  it('delete() issues DELETE with the key in the path', () => {
    service.delete(3).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('moveSlot() POSTs the target slot to the /{id}/move-slot sub-route', () => {
    let result: FeaturedPromoItem | undefined;
    service.moveSlot(1, 2).subscribe((item) => (result = item));

    const req = httpMock.expectOne(`${baseUrl}/1/move-slot`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ targetSlot: 2 });
    req.flush({ ...monday1, slot: 2 });

    expect(result?.slot).toBe(2);
  });

  it('surfaces a 409 from an occupied slot to the caller', () => {
    let status: number | undefined;
    service.create(request).subscribe({ error: (err) => (status = err.status) });

    httpMock.expectOne(baseUrl).flush('Conflict', { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });

  it('surfaces server errors to the caller', () => {
    let status: number | undefined;
    service.getById(99).subscribe({ error: (err) => (status = err.status) });

    httpMock.expectOne(`${baseUrl}/99`).flush('Not Found', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
