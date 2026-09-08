import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService, LOGIN_ROUTE } from '@core/services/auth.service';

/**
 * Blocks a route when session storage holds no access token, sending the user to the login page
 * with the requested URL kept as `returnUrl` so the sign-in lands where they were headed.
 *
 * This is convenience, not security: the API rejects an unauthenticated request whatever the
 * browser decided to render.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.hasToken()) return true;

  return router.createUrlTree([LOGIN_ROUTE], { queryParams: { returnUrl: state.url } });
};
