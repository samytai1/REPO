import { Routes } from '@angular/router';

import { authGuard } from '@core/guards/auth.guard';
import {
  passwordChangeGuard,
  unflaggedAwayFromForceGuard,
} from '@core/guards/password-change.guard';

/**
 * Root routes. Everything but `login` sits behind `authGuard`; a request without a token in session
 * storage is redirected to the login page with the requested URL as `returnUrl`.
 *
 * Every feature route then carries `passwordChangeGuard` as well, which holds a user still on the
 * 預設密碼 on `change-password`. That route is the one exception: it carries
 * `unflaggedAwayFromForceGuard` instead — giving it both would redirect a flagged user to the page
 * they are already on, and the `**` fallback would turn that into a loop.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('@features/auth/login/login').then((m) => m.Login),
    title: '登入 Login',
  },
  {
    path: 'change-password',
    canActivate: [unflaggedAwayFromForceGuard],
    loadComponent: () =>
      import('@features/auth/force-password-change/force-password-change').then(
        (m) => m.ForcePasswordChange,
      ),
    title: '變更密碼 Change Password',
  },
  {
    path: 'profile',
    canActivate: [authGuard, passwordChangeGuard],
    loadComponent: () => import('@features/profile/profile').then((m) => m.Profile),
    title: '個人資料 My Profile',
  },
  { path: '', pathMatch: 'full', redirectTo: 'featured-promo-items' },
  {
    path: 'app-roles',
    canActivate: [authGuard, passwordChangeGuard],
    loadChildren: () =>
      import('@features/app-roles/app-roles.routes').then((m) => m.appRoleRoutes),
  },
  {
    path: 'publish-statuses',
    canActivate: [authGuard, passwordChangeGuard],
    loadChildren: () =>
      import('@features/publish-statuses/publish-statuses.routes').then((m) => m.publishStatusRoutes),
  },
  {
    path: 'partners',
    canActivate: [authGuard, passwordChangeGuard],
    loadChildren: () => import('@features/partners/partners.routes').then((m) => m.partnerRoutes),
  },
  {
    path: 'courses',
    canActivate: [authGuard, passwordChangeGuard],
    loadChildren: () => import('@features/courses/courses.routes').then((m) => m.courseRoutes),
  },
  {
    path: 'featured-promo-items',
    canActivate: [authGuard, passwordChangeGuard],
    loadChildren: () =>
      import('@features/featured-promo-items/featured-promo-items.routes').then(
        (m) => m.featuredPromoItemRoutes,
      ),
  },
  { path: '**', redirectTo: 'featured-promo-items' },
];
