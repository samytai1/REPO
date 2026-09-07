/** 使用者 AppUser — slim lookup row used for the n-n AppUserRole option list. */
export interface AppUserLookup {
  /** 使用者代碼 */
  userId: string;
  /** 使用者名稱 */
  userName: string;
  /** 啟用 */
  isActive: boolean;
}
