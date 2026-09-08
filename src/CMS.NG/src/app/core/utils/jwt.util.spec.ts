import { ROLE_CLAIM, decodeJwtPayload, rolesFromToken } from './jwt.util';

/** Encodes a payload the way a real JWT does: base64url, no padding, with a fake signature. */
function tokenFor(payload: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`;
}

describe('jwt.util', () => {
  describe('decodeJwtPayload', () => {
    it('reads the claims of a well-formed token', () => {
      const payload = decodeJwtPayload(tokenFor({ sub: 'admin', userName: 'Admin User' }));

      expect(payload).toEqual({ sub: 'admin', userName: 'Admin User' });
    });

    it('decodes multi-byte claim values', () => {
      const payload = decodeJwtPayload(tokenFor({ userName: '王小明' }));

      expect(payload?.['userName']).toBe('王小明');
    });

    it('returns null for a missing, empty or non-JWT value', () => {
      expect(decodeJwtPayload(null)).toBeNull();
      expect(decodeJwtPayload(undefined)).toBeNull();
      expect(decodeJwtPayload('')).toBeNull();
      expect(decodeJwtPayload('not-a-jwt')).toBeNull();
      expect(decodeJwtPayload('only.two')).toBeNull();
    });

    it('returns null rather than throwing when the payload is not decodable JSON', () => {
      expect(decodeJwtPayload('header.@@@not-base64@@@.signature')).toBeNull();
      expect(decodeJwtPayload(`header.${btoa('"a string, not an object"')}.signature`)).toBeNull();
    });
  });

  describe('rolesFromToken', () => {
    it('reads several roles from an array claim', () => {
      expect(rolesFromToken(tokenFor({ [ROLE_CLAIM]: ['Admin', 'User'] }))).toEqual(['Admin', 'User']);
    });

    it('reads a single role, which the API serialises as a bare string', () => {
      expect(rolesFromToken(tokenFor({ [ROLE_CLAIM]: 'Admin' }))).toEqual(['Admin']);
    });

    it('reads no roles from a token that carries none', () => {
      expect(rolesFromToken(tokenFor({ sub: 'helen' }))).toEqual([]);
      expect(rolesFromToken(null)).toEqual([]);
      expect(rolesFromToken('garbage')).toEqual([]);
    });

    it('ignores non-string entries in the role claim', () => {
      expect(rolesFromToken(tokenFor({ [ROLE_CLAIM]: ['Admin', 7, null] }))).toEqual(['Admin']);
    });
  });
});
