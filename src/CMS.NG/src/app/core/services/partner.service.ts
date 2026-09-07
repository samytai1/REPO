import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import { Partner, PartnerQuery, PartnerRequest, PartnerUpdateRequest } from '@core/models';

/**
 * Data service for 合作夥伴 Partner.
 *
 * The record key (`pkid`) is a smallint, so single-record routes need no URL encoding.
 * `create` omits the key — the column is IDENTITY — while `update` carries it in the body, since
 * the API's PUT takes no route param.
 */
@Injectable({ providedIn: 'root' })
export class PartnerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/partners`;

  /** GET /api/partners */
  getAll(): Observable<Partner[]> {
    return this.http.get<Partner[]>(this.baseUrl);
  }

  /** POST /api/partners/query */
  query(query: PartnerQuery): Observable<Partner[]> {
    return this.http.post<Partner[]>(`${this.baseUrl}/query`, query);
  }

  /** GET /api/partners/{id} */
  getById(pkid: number): Observable<Partner> {
    return this.http.get<Partner>(`${this.baseUrl}/${pkid}`);
  }

  /** POST /api/partners — pkid is assigned by the database. */
  create(request: PartnerRequest): Observable<Partner> {
    return this.http.post<Partner>(this.baseUrl, request);
  }

  /** PUT /api/partners — the key travels in the body, not the route. */
  update(request: PartnerUpdateRequest): Observable<Partner> {
    return this.http.put<Partner>(this.baseUrl, request);
  }

  /** DELETE /api/partners/{id} */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
