import { Routes } from '@angular/router';

/** 發布狀態 PublishStatus feature routes. Lazily loaded from the root router. */
export const publishStatusRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./publish-status-list/publish-status-list').then((m) => m.PublishStatusList),
    title: '發布狀態 PublishStatus',
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./publish-status-form/publish-status-form').then((m) => m.PublishStatusForm),
    title: '新增發布狀態',
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./publish-status-form/publish-status-form').then((m) => m.PublishStatusForm),
    title: '編輯發布狀態',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./publish-status-detail/publish-status-detail').then((m) => m.PublishStatusDetail),
    title: '檢視發布狀態',
  },
];
