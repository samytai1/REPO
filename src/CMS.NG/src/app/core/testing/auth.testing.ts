import { AuthProfile } from '@core/models';
import { AUTH_SESSION_KEY } from '@core/services/auth.service';
import { ROLE_CLAIM } from '@core/utils/jwt.util';

/**
 * Test-only helpers for setting up a signed-in session. Not referenced by the application.
 *
 * The tokens are unsigned stand-ins: nothing in the browser verifies a signature, so a correctly
 * *shaped* token is all a spec needs. The API is what checks the real thing.
 */

/** Builds a JWT-shaped token whose payload carries `roles` as `role` claims. */
export function fakeAccessToken(roles: string[] = [], userId = 'admin@example.com'): string {
  const encode = (value: unknown) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  const payload: Record<string, unknown> = { sub: userId, userId };

  // One role serialises as a string, several as an array — exactly as the API emits them.
  if (roles.length === 1) payload[ROLE_CLAIM] = roles[0];
  if (roles.length > 1) payload[ROLE_CLAIM] = roles;

  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-signature`;
}

/** A profile shaped exactly as `POST /api/auth/login` returns one. */
export function fakeProfile(roles: string[] = [], userName = 'Admin User'): AuthProfile {
  return {
    userId: 'admin@example.com',
    userName,
    accessToken: fakeAccessToken(roles),
  };
}

/** Writes a signed-in session into session storage, as `AuthService.login` would. */
export function signIn(roles: string[] = ['Admin'], userName = 'Admin User'): AuthProfile {
  const profile = fakeProfile(roles, userName);
  sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify(profile));
  return profile;
}
