/** 促銷 — slim FK reference, mirrors CMS.API `FeaturedPromoItemPromotionRef`. */
export interface FeaturedPromoItemPromotionRef {
  pkid: number;
  promoCode: string;
}

/** 教育中心 — slim FK reference, mirrors CMS.API `FeaturedPromoItemTrainingCenterRef`. */
export interface FeaturedPromoItemTrainingCenterRef {
  pkid: number;
  name: string;
}

/** 上稿作業 FeaturedPromoItem — response model, mirrors CMS.API `FeaturedPromoItem`. */
export interface FeaturedPromoItem {
  /** 主代碼 — int IDENTITY, assigned by the database */
  pkid: number;
  /** 上稿日期 — ISO `yyyy-MM-dd` */
  scheduleOn: string;
  /** 教育中心 — FK key */
  trainingCenterPkid: number;
  /** 版位 — 1, 2 or 3 */
  slot: number;
  /** 促銷 — FK key */
  promotionPkid: number;
  /** 主題 */
  topic: string;
  /** 說明 */
  description: string;
  /** 促銷 — nav object carrying the PromoCode the grid shows */
  promotion: FeaturedPromoItemPromotionRef | null;
  /** 教育中心 — nav object */
  trainingCenter: FeaturedPromoItemTrainingCenterRef | null;
}

/** 上稿作業 — create DTO. `pkid` is IDENTITY, so it is not sent. */
export interface FeaturedPromoItemRequest {
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
}

/** 上稿作業 — update DTO. PUT posts to the collection route, so the key travels in the body. */
export interface FeaturedPromoItemUpdateRequest extends FeaturedPromoItemRequest {
  pkid: number;
}

/** 上稿作業 — search DTO: one training center, one Monday-to-Sunday week. */
export interface FeaturedPromoItemQuery {
  trainingCenterPkid?: number | null;
  /** Any ISO date inside the week; the API snaps it to Monday–Sunday */
  weekOf?: string | null;
}

/** Body of `POST /api/featured-promo-items/{id}/move-slot`. */
export interface FeaturedPromoItemMoveRequest {
  targetSlot: number;
}

/** The three fixed slots of the grid, in display order. */
export const FEATURED_PROMO_SLOTS: readonly number[] = [1, 2, 3];

/**
 * What Copy captures and Paste replays — the promotion and the two text columns. The target
 * day / center / slot always come from the cell being pasted into.
 */
export interface FeaturedPromoItemClipboard {
  promotionPkid: number;
  promoCode: string;
  topic: string;
  description: string;
}
