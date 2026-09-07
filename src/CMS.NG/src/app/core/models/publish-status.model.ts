/** 發布狀態 PublishStatus — response model, mirrors CMS.API `PublishStatus`. */
export interface PublishStatus {
  /** 主代碼 — business key, a tinyint the user supplies (not an IDENTITY column) */
  pkid: number;
  /** 狀態說明 */
  description: string;
  /** 草稿 */
  isDraft: boolean;
  /** 已發布 */
  isPublished: boolean;
  /** 已下架 */
  isDiscontinued: boolean;
}

/** 發布狀態 PublishStatus — write DTO. `pkid` is the key: editable on add, read-only on edit. */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** 發布狀態 PublishStatus — search DTO. */
export interface PublishStatusQuery {
  keyword?: string | null;
  pkidFrom?: number | null;
  pkidTo?: number | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

/** A PublishStatusQuery with every filter cleared. */
export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = {
  keyword: null,
  pkidFrom: null,
  pkidTo: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null,
};
