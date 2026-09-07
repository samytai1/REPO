import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import { PublishStatus, PublishStatusQuery, PublishStatusRequest } from '@core/models';

/**
 * Data service for 發布狀態 PublishStatus.
 *
 * The record key (`pkid`) is a tinyint, so single-record routes need no URL encoding.
 * `update` carries the key in the body — the API's PUT takes no route param.
 */
@Injectable({ providedIn: 'root' })
export class PublishStatusService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/publish-statuses`;

  /** GET /api/publish-statuses */
  getAll(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(this.baseUrl);
  }

  /** POST /api/publish-statuses/query */
  query(query: PublishStatusQuery): Observable<PublishStatus[]> {
    return this.http.post<PublishStatus[]>(`${this.baseUrl}/query`, query);
  }

  /** GET /api/publish-statuses/{id} */
  getById(pkid: number): Observable<PublishStatus> {
    return this.http.get<PublishStatus>(`${this.baseUrl}/${pkid}`);
  }

  /** POST /api/publish-statuses */
  create(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.post<PublishStatus>(this.baseUrl, request);
  }

  /** PUT /api/publish-statuses — the key travels in the body, not the route. */
  update(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.put<PublishStatus>(this.baseUrl, request);
  }

  /** DELETE /api/publish-statuses/{id} */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
