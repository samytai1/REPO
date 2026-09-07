import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { AppUserLookup, PublishStatus } from '@core/models';
import { LookupService } from './lookup.service';

describe('LookupService', () => {
  let service: LookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), LookupService],
    });

    service = TestBed.inject(LookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAppUsers() issues GET /api/lookups/app-users', () => {
    const users: AppUserLookup[] = [{ userId: 'helen', userName: 'helen', isActive: true }];

    let result: AppUserLookup[] | undefined;
    service.getAppUsers().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/app-users`);
    expect(req.request.method).toBe('GET');
    req.flush(users);

    expect(result).toEqual(users);
  });

  it('getPublishStatuses() issues GET /api/lookups/publish-statuses', () => {
    const statuses: PublishStatus[] = [
      { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
      { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
    ];

    let result: PublishStatus[] | undefined;
    service.getPublishStatuses().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/publish-statuses`);
    expect(req.request.method).toBe('GET');
    req.flush(statuses);

    expect(result).toEqual(statuses);
  });
});
