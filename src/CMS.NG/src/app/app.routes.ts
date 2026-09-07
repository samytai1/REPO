import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app-roles' },
  {
    path: 'app-roles',
    loadChildren: () =>
      import('@features/app-roles/app-roles.routes').then((m) => m.appRoleRoutes),
  },
  { path: '**', redirectTo: 'app-roles' },
];
