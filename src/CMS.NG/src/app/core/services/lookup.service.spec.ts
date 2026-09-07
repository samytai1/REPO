import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { AppUserLookup, Partner, PublishStatus } from '@core/models';
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

  it('getPartners() issues GET /api/lookups/partners', () => {
    const partners: Partner[] = [
      {
        pkid: 2,
        name: '思科',
        appKey: 'CISCO',
        nameOnPartnerMenu: 'Cisco 思科課程',
        nameOnCourseDetailPage: '思科',
        displayOrder: 10,
        imageFilename: 'cisco.png',
      },
      {
        pkid: 1,
        name: '微軟',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft 微軟課程',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 20,
        imageFilename: null,
      },
    ];

    let result: Partner[] | undefined;
    service.getPartners().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/partners`);
    expect(req.request.method).toBe('GET');
    req.flush(partners);

    expect(result).toEqual(partners);
  });
});
