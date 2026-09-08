/**
 * Reads the claims out of a JWT **without** verifying it.
 *
 * The API is the only thing that may trust a token; the browser decodes it purely to decide what to
 * put on screen (the sidebar's Admin group). A forged token still buys nothing — every endpoint
 * behind it validates the signature server-side.
 */

/** Claim type carrying each dbo.AppUserRole.RoleId — matches `JwtTokenService.RoleClaimType`. */
export const ROLE_CLAIM = 'role';

/** The decoded payload of `token`, or `null` when it is missing or not a readable JWT. */
export function decodeJwtPayload(token: string | null | undefined): Record<string, unknown> | null {
  if (!token) return null;

  const segments = token.split('.');
  if (segments.length !== 3) return null;

  try {
    const json = decodeBase64Url(segments[1]);
    const payload: unknown = JSON.parse(json);

    return typeof payload === 'object' && payload !== null
      ? (payload as Record<string, unknown>)
      : null;
  } catch {
    // Truncated, re-encoded or simply not a JWT — treat it as carrying no claims at all.
    return null;
  }
}

/**
 * The `role` claims in `token`. A single role serialises as a string and several as an array, so
 * both shapes are flattened to the same list; anything else reads as no roles.
 */
export function rolesFromToken(token: string | null | undefined): string[] {
  const claim = decodeJwtPayload(token)?.[ROLE_CLAIM];

  if (typeof claim === 'string') return [claim];

  return Array.isArray(claim) ? claim.filter((role): role is string => typeof role === 'string') : [];
}

/** base64url → UTF-8 text. `atob` only understands base64, and only gives back bytes. */
function decodeBase64Url(segment: string): string {
  const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

  const bytes = atob(padded);

  // Percent-escape each byte so multi-byte UTF-8 (a Chinese UserName) survives the round trip.
  return decodeURIComponent(
    Array.from(bytes, (char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`).join(''),
  );
}
