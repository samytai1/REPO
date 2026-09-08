import { Routes } from '@angular/router';

import { authGuard } from '@core/guards/auth.guard';

/**
 * Root routes. Everything but `login` sits behind `authGuard`; a request without a token in session
 * storage is redirected to the login page with the requested URL as `returnUrl`.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('@features/auth/login/login').then((m) => m.Login),
    title: '登入 Login',
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('@features/profile/profile').then((m) => m.Profile),
    title: '個人資料 My Profile',
  },
  { path: '', pathMatch: 'full', redirectTo: 'featured-promo-items' },
  {
    path: 'app-roles',
    canActivate: [authGuard],
    loadChildren: () =>
      import('@features/app-roles/app-roles.routes').then((m) => m.appRoleRoutes),
  },
  {
    path: 'publish-statuses',
    canActivate: [authGuard],
    loadChildren: () =>
      import('@features/publish-statuses/publish-statuses.routes').then((m) => m.publishStatusRoutes),
  },
  {
    path: 'partners',
    canActivate: [authGuard],
    loadChildren: () => import('@features/partners/partners.routes').then((m) => m.partnerRoutes),
  },
  {
    path: 'courses',
    canActivate: [authGuard],
    loadChildren: () => import('@features/courses/courses.routes').then((m) => m.courseRoutes),
  },
  {
    path: 'featured-promo-items',
    canActivate: [authGuard],
    loadChildren: () =>
      import('@features/featured-promo-items/featured-promo-items.routes').then(
        (m) => m.featuredPromoItemRoutes,
      ),
  },
  { path: '**', redirectTo: 'featured-promo-items' },
];
