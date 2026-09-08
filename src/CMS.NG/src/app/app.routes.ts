import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app-roles' },
  {
    path: 'app-roles',
    loadChildren: () =>
      import('@features/app-roles/app-roles.routes').then((m) => m.appRoleRoutes),
  },
  {
    path: 'publish-statuses',
    loadChildren: () =>
      import('@features/publish-statuses/publish-statuses.routes').then((m) => m.publishStatusRoutes),
  },
  {
    path: 'partners',
    loadChildren: () => import('@features/partners/partners.routes').then((m) => m.partnerRoutes),
  },
  {
    path: 'courses',
    loadChildren: () => import('@features/courses/courses.routes').then((m) => m.courseRoutes),
  },
  {
    path: 'featured-promo-items',
    loadChildren: () =>
      import('@features/featured-promo-items/featured-promo-items.routes').then(
        (m) => m.featuredPromoItemRoutes,
      ),
  },
  { path: '**', redirectTo: 'app-roles' },
];
