import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
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

/** Slim lookup lists used to fill FK / n-n option controls. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  /** GET /api/lookups/app-users */
  getAppUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  /** GET /api/lookups/publish-statuses — option label is `description`, ordered by `pkid`. */
  getPublishStatuses(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(`${this.baseUrl}/publish-statuses`);
  }

  /** GET /api/lookups/partners — option label is `name`, ordered by `displayOrder`. */
  getPartners(): Observable<Partner[]> {
    return this.http.get<Partner[]>(`${this.baseUrl}/partners`);
  }

  /** GET /api/lookups/course-groups — option label is `description`, ordered by `description`. */
  getCourseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }

  /** GET /api/lookups/job-categories — option label is `description`, ordered by `description`. */
  getJobCategories(): Observable<JobCategoryLookup[]> {
    return this.http.get<JobCategoryLookup[]>(`${this.baseUrl}/job-categories`);
  }

  /** GET /api/lookups/certifications — option label is `title`, ordered by `title`. */
  getCertifications(): Observable<CertificationLookup[]> {
    return this.http.get<CertificationLookup[]>(`${this.baseUrl}/certifications`);
  }

  /** GET /api/lookups/training-centers — option label is `name`, ordered by `displayOrder`. */
  getTrainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.baseUrl}/training-centers`);
  }

  /**
   * GET /api/lookups/promotions?keyword= — PromoCode prefix search, newest code first, at most
   * 20 rows. A blank keyword is not sent.
   */
  getPromotions(keyword: string | null | undefined): Observable<Promotion2Lookup[]> {
    const trimmed = keyword?.trim() ?? '';
    const params = trimmed ? new HttpParams().set('keyword', trimmed) : new HttpParams();
    return this.http.get<Promotion2Lookup[]>(`${this.baseUrl}/promotions`, { params });
  }
}
