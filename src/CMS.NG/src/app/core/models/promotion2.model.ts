/**
 * 促銷 Promotion2 — slim lookup row for the PromoCode autocomplete on the 上稿作業 form.
 * Topic and Description ride along so the form can pre-fill its own copies of them.
 */
export interface Promotion2Lookup {
  /** 主代碼 */
  pkid: number;
  /** 促銷代碼 — UNIQUE */
  promoCode: string;
  /** 主題 */
  topic: string;
  /** 說明 */
  description: string;
}
