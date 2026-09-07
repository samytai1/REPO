import { Routes } from '@angular/router';

/** 角色 AppRole feature routes. Lazily loaded from the root router. */
export const appRoleRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./app-role-list/app-role-list').then((m) => m.AppRoleList),
    title: '角色 AppRole',
  },
  {
    path: 'new',
    loadComponent: () => import('./app-role-form/app-role-form').then((m) => m.AppRoleForm),
    title: '新增角色',
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./app-role-form/app-role-form').then((m) => m.AppRoleForm),
    title: '編輯角色',
  },
  {
    path: ':id',
    loadComponent: () => import('./app-role-detail/app-role-detail').then((m) => m.AppRoleDetail),
    title: '檢視角色',
  },
];
