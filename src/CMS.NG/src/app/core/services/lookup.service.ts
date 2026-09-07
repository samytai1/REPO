import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import { AppUserLookup, PublishStatus } from '@core/models';

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
}
