import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService, LOGIN_ROUTE } from '@core/services/auth.service';

/**
 * Turns a 401 from the API into a sign-out: the session is cleared and the user lands on the login
 * page, with the page they were on kept as `returnUrl`.
 *
 * The login call itself is exempt — its 401 means "wrong password", not "your session expired", and
 * the login page shows that message itself. The error is always re-thrown so the caller still sees
 * it.
 */
export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLoginRequest(req.url)) {
        auth.clearSession();

        const returnUrl = router.url;

        void router.navigate([LOGIN_ROUTE], {
          // Bouncing back to the login page after logging in would be a loop.
          queryParams: returnUrl.startsWith(LOGIN_ROUTE) ? {} : { returnUrl },
        });
      }

      return throwError(() => error);
    }),
  );
};

function isLoginRequest(url: string): boolean {
  return url.endsWith('/auth/login');
}
