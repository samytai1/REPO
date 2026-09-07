/** 合作夥伴 Partner — response model, mirrors CMS.API `Partner`. */
export interface Partner {
  /** 主代碼 — smallint IDENTITY, assigned by the database */
  pkid: number;
  /** 名稱 */
  name: string;
  /** 應用代碼 */
  appKey: string;
  /** 選單顯示名稱 */
  nameOnPartnerMenu: string;
  /** 課程明細頁名稱 */
  nameOnCourseDetailPage: string;
  /** 顯示順序 */
  displayOrder: number;
  /** 圖片檔名 — nullable */
  imageFilename: string | null;
}

/** 合作夥伴 Partner — create DTO. `pkid` is IDENTITY, so it is not sent. */
export interface PartnerRequest {
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

/** 合作夥伴 Partner — update DTO. PUT posts to the collection route, so the key travels in the body. */
export interface PartnerUpdateRequest extends PartnerRequest {
  pkid: number;
}

/** 合作夥伴 Partner — search DTO. */
export interface PartnerQuery {
  keyword?: string | null;
  displayOrderFrom?: number | null;
  displayOrderTo?: number | null;
  /** 圖片 — true keeps only rows with an image, false only rows without, null is unfiltered. */
  hasImage?: boolean | null;
}

/** A PartnerQuery with every filter cleared. */
export const EMPTY_PARTNER_QUERY: PartnerQuery = {
  keyword: null,
  displayOrderFrom: null,
  displayOrderTo: null,
  hasImage: null,
};
