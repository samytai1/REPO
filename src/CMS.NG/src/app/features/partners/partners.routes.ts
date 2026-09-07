import { Routes } from '@angular/router';

/** 合作夥伴 Partner feature routes. Lazily loaded from the root router. */
export const partnerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./partner-list/partner-list').then((m) => m.PartnerList),
    title: '合作夥伴 Partner',
  },
  {
    path: 'new',
    loadComponent: () => import('./partner-form/partner-form').then((m) => m.PartnerForm),
    title: '新增合作夥伴',
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./partner-form/partner-form').then((m) => m.PartnerForm),
    title: '編輯合作夥伴',
  },
  {
    path: ':id',
    loadComponent: () => import('./partner-detail/partner-detail').then((m) => m.PartnerDetail),
    title: '檢視合作夥伴',
  },
];
