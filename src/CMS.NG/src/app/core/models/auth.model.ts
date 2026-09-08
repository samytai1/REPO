/** 登入 — request body for `POST /api/auth/login`. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/**
 * 登入結果 — exactly what the API returns and what the app keeps in **session** storage.
 *
 * There is no `roles` field: the roles travel inside `accessToken` as `role` claims, and
 * `AuthService` reads them from there rather than asking the API a second time.
 */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
}

/** 個人資料 — request body for `PUT /api/auth/profile`. */
export interface UpdateProfileRequest {
  userName: string;
}

/**
 * 個人資料 — exactly what `PUT /api/auth/profile` returns.
 *
 * Like {@link AuthProfile} it has no `roles` field: the roles ride in the access token, and the
 * token is not re-issued by a rename, so nothing about them changes.
 */
export interface UserProfile {
  userId: string;
  userName: string;
}

/**
 * 變更密碼 — request body for `PUT /api/auth/password`.
 *
 * No `userId`: the API takes the account from the token. No confirm field either — re-typing the
 * new password is checked in the form, and sending it would only put the secret on the wire twice.
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
