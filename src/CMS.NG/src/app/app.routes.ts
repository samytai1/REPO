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
  { path: '**', redirectTo: 'app-roles' },
];
