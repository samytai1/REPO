import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '@environments/environment';

import { AuthService } from '@core/services/auth.service';

/**
 * Attaches `Authorization: Bearer <accessToken>` to every call to this API.
 *
 * The token comes from session storage on each request, so a sign-out in another tab takes effect
 * immediately. Requests to anywhere else are left alone — the token is for our API only.
 */
export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token();

  if (token === null || !req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
