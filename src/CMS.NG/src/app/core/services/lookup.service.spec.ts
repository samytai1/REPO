import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import {
  AppUserLookup,
  CertificationLookup,
  CourseGroupLookup,
  JobCategoryLookup,
  Partner,
  Promotion2Lookup,
  PublishStatus,
  TrainingCenterLookup,
} from '@core/models';
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

  it('getCourseGroups() issues GET /api/lookups/course-groups', () => {
    const groups: CourseGroupLookup[] = [
      { pkid: 20, description: '資安' },
      { pkid: 10, description: '雲端' },
    ];

    let result: CourseGroupLookup[] | undefined;
    service.getCourseGroups().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/course-groups`);
    expect(req.request.method).toBe('GET');
    req.flush(groups);

    expect(result).toEqual(groups);
  });

  it('getJobCategories() issues GET /api/lookups/job-categories', () => {
    const categories: JobCategoryLookup[] = [
      { pkid: 5, description: '系統管理' },
      { pkid: 6, description: '網路管理' },
    ];

    let result: JobCategoryLookup[] | undefined;
    service.getJobCategories().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/job-categories`);
    expect(req.request.method).toBe('GET');
    req.flush(categories);

    expect(result).toEqual(categories);
  });

  it('getCertifications() issues GET /api/lookups/certifications', () => {
    const certifications: CertificationLookup[] = [
      { pkid: 100, title: 'AZ-104', partnerPkid: 1 },
      { pkid: 300, title: null, partnerPkid: 2 },
    ];

    let result: CertificationLookup[] | undefined;
    service.getCertifications().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/certifications`);
    expect(req.request.method).toBe('GET');
    req.flush(certifications);

    expect(result).toEqual(certifications);
  });

  it('getTrainingCenters() issues GET /api/lookups/training-centers', () => {
    const centers: TrainingCenterLookup[] = [
      { pkid: 1, name: '台北', displayOrder: 1 },
      { pkid: 2, name: '新竹', displayOrder: 2 },
    ];

    let result: TrainingCenterLookup[] | undefined;
    service.getTrainingCenters().subscribe((value) => (result = value));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/training-centers`);
    expect(req.request.method).toBe('GET');
    req.flush(centers);

    expect(result).toEqual(centers);
  });

  it('getPromotions() issues GET /api/lookups/promotions with the trimmed keyword as a query param', () => {
    const promotions: Promotion2Lookup[] = [
      { pkid: 12, promoCode: '20251215_n8n', topic: 'n8n自動化三部曲', description: '從自動化新手' },
    ];

    let result: Promotion2Lookup[] | undefined;
    service.getPromotions('  2025 ').subscribe((value) => (result = value));

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiBaseUrl}/lookups/promotions` && r.params.get('keyword') === '2025',
    );
    expect(req.request.method).toBe('GET');
    req.flush(promotions);

    expect(result).toEqual(promotions);
  });

  it('getPromotions() sends no keyword param when the keyword is blank', () => {
    service.getPromotions('   ').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/promotions`);
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
  });
});
