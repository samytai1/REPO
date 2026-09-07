import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models';

/**
 * Data service for 角色 AppRole.
 *
 * The record key (`roleId`) is an nvarchar, so every single-record route is
 * URL-encoded. `update` carries the key in the body — the API's PUT takes no route param.
 */
@Injectable({ providedIn: 'root' })
export class AppRoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/app-roles`;

  /** GET /api/app-roles */
  getAll(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.baseUrl);
  }

  /** POST /api/app-roles/query */
  query(query: AppRoleQuery): Observable<AppRole[]> {
    return this.http.post<AppRole[]>(`${this.baseUrl}/query`, query);
  }

  /** GET /api/app-roles/{id} */
  getById(roleId: string): Observable<AppRole> {
    return this.http.get<AppRole>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }

  /** POST /api/app-roles */
  create(request: AppRoleRequest): Observable<AppRole> {
    return this.http.post<AppRole>(this.baseUrl, request);
  }

  /** PUT /api/app-roles — pkid/key travels in the body, not the route. */
  update(request: AppRoleRequest): Observable<AppRole> {
    return this.http.put<AppRole>(this.baseUrl, request);
  }

  /** DELETE /api/app-roles/{id} */
  delete(roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(roleId)}`);
  }
}
