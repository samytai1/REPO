import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import { Course, CourseQuery, CourseRequest, CourseUpdateRequest } from '@core/models';

/**
 * Data service for 課程 Course.
 *
 * The record key (`pkid`) is an int, so single-record routes need no URL encoding.
 * `create` omits the key — the column is IDENTITY — while `update` carries it in the body, since
 * the API's PUT takes no route param.
 */
@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/courses`;

  /** GET /api/courses */
  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  /** POST /api/courses/query */
  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, query);
  }

  /** GET /api/courses/{id} — includes both n-n collections. */
  getById(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${pkid}`);
  }

  /** POST /api/courses — pkid is assigned by the database. */
  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** PUT /api/courses — the key travels in the body, not the route. */
  update(request: CourseUpdateRequest): Observable<Course> {
    return this.http.put<Course>(this.baseUrl, request);
  }

  /** DELETE /api/courses/{id} */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
