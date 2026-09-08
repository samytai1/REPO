import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

import {
  FeaturedPromoItem,
  FeaturedPromoItemMoveRequest,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
  FeaturedPromoItemUpdateRequest,
} from '@core/models';

/**
 * Data service for 上稿作業 FeaturedPromoItem.
 *
 * The record key (`pkid`) is an int, so single-record routes need no URL encoding.
 * `create` omits the key — the column is IDENTITY — while `update` carries it in the body, since
 * the API's PUT takes no route param. `moveSlot` is the grid's + / − action.
 */
@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;

  /** GET /api/featured-promo-items — every row; the grid never needs this. */
  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  /** POST /api/featured-promo-items/query — one center, one week. */
  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, query);
  }

  /** GET /api/featured-promo-items/{id} */
  getById(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${pkid}`);
  }

  /** POST /api/featured-promo-items — pkid is assigned by the database; 409 when the slot is taken. */
  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** PUT /api/featured-promo-items — the key travels in the body, not the route. */
  update(request: FeaturedPromoItemUpdateRequest): Observable<FeaturedPromoItem> {
    return this.http.put<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** DELETE /api/featured-promo-items/{id} */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** POST /api/featured-promo-items/{id}/move-slot — swaps with the occupant when the slot is taken. */
  moveSlot(pkid: number, targetSlot: number): Observable<FeaturedPromoItem> {
    const body: FeaturedPromoItemMoveRequest = { targetSlot };
    return this.http.post<FeaturedPromoItem>(`${this.baseUrl}/${pkid}/move-slot`, body);
  }
}
