import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '@environments/environment';

import { AuthProfile, LoginRequest, UpdateProfileRequest, UserProfile } from '@core/models';
import { rolesFromToken } from '@core/utils/jwt.util';

/** Session-storage slot holding the signed-in profile. */
export const AUTH_SESSION_KEY = 'cms-auth';

/** The public page. Everything else is behind `authGuard`. */
export const LOGIN_ROUTE = '/login';

/** 個人資料 — the signed-in user's own page, reached from the app shell's user menu. */
export const PROFILE_ROUTE = '/profile';

/** Where a successful sign-in lands when there is no `returnUrl` to honour. */
export const DEFAULT_ROUTE = '/featured-promo-items';

/**
 * 登入 Auth — the signed-in session.
 *
 * The profile lives in **session** storage, so it dies with the tab; a new tab signs in again.
 * Storage is the source of truth for "is there a token?" — another tab may have cleared it — while
 * the `profile` signal mirrors it for the templates that render the user's name and menu.
 *
 * Roles are read out of the token's `role` claims (`@core/utils/jwt.util`), never from a second API
 * call. They gate what the sidebar *shows*; the API re-validates every request regardless.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  private readonly signedIn = signal<AuthProfile | null>(readStoredProfile());

  /** The stored profile, or `null` when nobody is signed in. */
  readonly profile = this.signedIn.asReadonly();

  /** 帳號 of the signed-in user. Read-only everywhere — only a fresh token can change it. */
  readonly userId = computed(() => this.signedIn()?.userId ?? '');

  /** 使用者名稱 for the app header. Empty when signed out. */
  readonly userName = computed(() => this.signedIn()?.userName ?? '');

  /** The roles carried by the current token, in the order the API issued them. */
  readonly roles = computed(() => rolesFromToken(this.signedIn()?.accessToken));

  /** POST /api/auth/login — on success the profile is stored before the caller sees it. */
  login(request: LoginRequest): Observable<AuthProfile> {
    return this.http
      .post<AuthProfile>(`${this.baseUrl}/login`, request)
      .pipe(tap((profile) => this.store(profile)));
  }

  /**
   * PUT /api/auth/profile — renames the signed-in user.
   *
   * The API takes the account from the bearer token, so the request carries the new name and
   * nothing else. On success the stored profile's `userName` is refreshed in place — which is what
   * updates the shell header — while `accessToken` is kept as it is: the API does not re-issue one,
   * so `userId` and the roles are untouched.
   */
  updateUserName(userName: string): Observable<UserProfile> {
    return this.http
      .put<UserProfile>(`${this.baseUrl}/profile`, { userName } satisfies UpdateProfileRequest)
      .pipe(
        tap((updated) => {
          const current = this.signedIn();
          if (current) this.store({ ...current, userName: updated.userName });
        }),
      );
  }

  /** The access token to send as `Authorization: Bearer …`, read from storage on every call. */
  token(): string | null {
    return readStoredProfile()?.accessToken ?? null;
  }

  /** Whether a token is in session storage. This — not the signal — is what the guard asks. */
  hasToken(): boolean {
    return this.token() !== null;
  }

  /** Role names are compared case-insensitively, as SQL Server compares them. */
  hasRole(role: string): boolean {
    return this.roles().some((assigned) => assigned.toLowerCase() === role.toLowerCase());
  }

  /**
   * Drops the whole session: the profile **and** every list page's stored filters, sort and paging.
   * Signing out must not leave the next user looking at the previous one's view.
   */
  clearSession(): void {
    try {
      sessionStorage.clear();
    } catch {
      /* storage unavailable — the in-memory signal below is still cleared */
    }

    this.signedIn.set(null);
  }

  private store(profile: AuthProfile): void {
    try {
      sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify(profile));
    } catch {
      /* storage unavailable — the session lasts until the page is reloaded */
    }

    this.signedIn.set(profile);
  }
}

/** Reads the profile back, tolerating an absent, unreadable or half-written slot. */
function readStoredProfile(): AuthProfile | null {
  try {
    const raw = sessionStorage.getItem(AUTH_SESSION_KEY);
    if (!raw) return null;

    const parsed: unknown = JSON.parse(raw);

    return isProfile(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function isProfile(value: unknown): value is AuthProfile {
  if (typeof value !== 'object' || value === null) return false;

  const candidate = value as Partial<AuthProfile>;

  return (
    typeof candidate.userId === 'string' &&
    typeof candidate.userName === 'string' &&
    typeof candidate.accessToken === 'string' &&
    candidate.accessToken !== ''
  );
}
