import { HttpErrorResponse } from '@angular/common/http';

import { problemDetail } from './problem-detail.util';

describe('problemDetail', () => {
  const FALLBACK = '密碼更新失敗，請稍後再試。';

  /** A rejection shaped exactly as the API sends one. */
  const badRequest = (body: unknown) =>
    new HttpErrorResponse({ status: 400, statusText: 'Bad Request', error: body });

  it('shows the server reason carried by a 400', () => {
    expect(problemDetail(badRequest({ detail: '目前密碼不正確。' }), FALLBACK)).toBe('目前密碼不正確。');
  });

  it('shows the policy message the same way', () => {
    const message = '新密碼至少 8 個字元，且需包含大寫字母、小寫字母、數字與符號。';

    expect(problemDetail(badRequest({ detail: message }), FALLBACK)).toBe(message);
  });

  it('falls back when the 400 carries no usable detail', () => {
    expect(problemDetail(badRequest({ detail: '   ' }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(badRequest({ detail: '' }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(badRequest({ detail: 42 }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(badRequest({}), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(badRequest(null), FALLBACK)).toBe(FALLBACK);
  });

  it('falls back rather than throwing when the 400 body is not an object', () => {
    expect(problemDetail(badRequest('not an object'), FALLBACK)).toBe(FALLBACK);
  });

  it('falls back for every status but 400', () => {
    // A 401 never carries a message for the user — the interceptor signs them out instead — and a
    // 500 has nothing worth showing.
    for (const status of [401, 403, 404, 500]) {
      const error = new HttpErrorResponse({ status, error: { detail: '不該顯示。' } });

      expect(problemDetail(error, FALLBACK)).toBe(FALLBACK);
    }
  });

  it('falls back for anything that is not an HTTP error at all', () => {
    expect(problemDetail(new Error('boom'), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(null, FALLBACK)).toBe(FALLBACK);
    expect(problemDetail({ detail: '不該顯示。' }, FALLBACK)).toBe(FALLBACK);
  });
});
