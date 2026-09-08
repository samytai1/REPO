import { AuthProfile } from '@core/models';
import { AUTH_SESSION_KEY } from '@core/services/auth.service';
import { MUST_CHANGE_PASSWORD_CLAIM, ROLE_CLAIM } from '@core/utils/jwt.util';

/**
 * Test-only helpers for setting up a signed-in session. Not referenced by the application.
 *
 * The tokens are unsigned stand-ins: nothing in the browser verifies a signature, so a correctly
 * *shaped* token is all a spec needs. The API is what checks the real thing.
 */

/**
 * Builds a JWT-shaped token whose payload carries `roles` as `role` claims.
 *
 * `mustChangePassword` is last and defaults to false, so every existing call site keeps meaning
 * exactly what it did. When set, the claim is written as the **string** `'true'` — the shape the
 * API really emits, so a spec cannot pass against a decoder that a real token would fail.
 */
export function fakeAccessToken(
  roles: string[] = [],
  userId = 'admin@example.com',
  mustChangePassword = false,
): string {
  const encode = (value: unknown) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  const payload: Record<string, unknown> = { sub: userId, userId };

  // One role serialises as a string, several as an array — exactly as the API emits them.
  if (roles.length === 1) payload[ROLE_CLAIM] = roles[0];
  if (roles.length > 1) payload[ROLE_CLAIM] = roles;

  // Only ever present when true, exactly as JwtTokenService writes it.
  if (mustChangePassword) payload[MUST_CHANGE_PASSWORD_CLAIM] = 'true';

  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-signature`;
}

/** A profile shaped exactly as `POST /api/auth/login` returns one. */
export function fakeProfile(
  roles: string[] = [],
  userName = 'Admin User',
  mustChangePassword = false,
): AuthProfile {
  return {
    userId: 'admin@example.com',
    userName,
    accessToken: fakeAccessToken(roles, 'admin@example.com', mustChangePassword),
  };
}

/** Writes a signed-in session into session storage, as `AuthService.login` would. */
export function signIn(
  roles: string[] = ['Admin'],
  userName = 'Admin User',
  mustChangePassword = false,
): AuthProfile {
  const profile = fakeProfile(roles, userName, mustChangePassword);
  sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify(profile));
  return profile;
}
