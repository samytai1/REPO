import { HttpErrorResponse } from '@angular/common/http';

/**
 * Unwraps the message the API meant the user to read.
 *
 * A refused password change comes back as a **400** carrying the server's own Chinese reason in
 * `ProblemDetails.detail` — 目前密碼不正確。, 新密碼不可與目前密碼相同。, or the strength rule — and
 * that text is written for the user, so it is shown verbatim. Anything else (a 500, a network
 * failure, a 400 with no usable detail) gets `fallback`.
 *
 * Shared rather than duplicated: both 個人資料 and 變更密碼 depend on this exact rule, and the
 * 400-vs-401 contract it implements is a pinned invariant of the auth surface. Two copies would
 * drift, and the page most likely to hit an unusual detail is the forced one.
 */
export function problemDetail(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse && error.status === 400) {
    // A 400 whose body is a bare string, or null, has no detail to read — the cast alone is not
    // enough, so the type of what comes back is checked too.
    const detail = (error.error as { detail?: unknown } | null)?.detail;

    if (typeof detail === 'string' && detail.trim() !== '') return detail;
  }

  return fallback;
}
