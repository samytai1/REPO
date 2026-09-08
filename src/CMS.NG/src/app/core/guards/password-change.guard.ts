import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import {
  AuthService,
  DEFAULT_ROUTE,
  FORCE_PASSWORD_CHANGE_ROUTE,
  LOGIN_ROUTE,
} from '@core/services/auth.service';

/**
 * Holds a user whose token says they are still on the 預設密碼 on the 變更密碼 page.
 *
 * This is convenience, not security: the API answers 403 to every route but `PUT /api/auth/password`
 * for such a token, whatever the browser decided to render. Without the guard the user would simply
 * see a working-looking app in which nothing loads.
 *
 * It runs **after** `authGuard`, and deliberately lets a signed-*out* user through: `authGuard` owns
 * that case and sends them to /login with a `returnUrl`. If this one redirected them too, a
 * signed-out user would land on 變更密碼 instead.
 *
 * No `returnUrl` is carried. A successful forced change signs the user out, so there is nothing to
 * return to — and a stored one would fight the login page's own redirect for a flagged sign-in.
 */
export const passwordChangeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.requiresPasswordChange()) return true;

  return router.createUrlTree([FORCE_PASSWORD_CHANGE_ROUTE]);
};

/**
 * The inverse, guarding the 變更密碼 route itself: only a signed-in, flagged user belongs there.
 *
 * Anyone else is sent away — signed out to /login, unflagged to `DEFAULT_ROUTE` — so the page cannot
 * be reached by typing its URL. It must **not** also carry {@link passwordChangeGuard}, or a flagged
 * user would be redirected to the page they are already on.
 */
export const unflaggedAwayFromForceGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.hasToken()) return router.createUrlTree([LOGIN_ROUTE]);

  return auth.requiresPasswordChange() ? true : router.createUrlTree([DEFAULT_ROUTE]);
};
