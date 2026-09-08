import { Routes } from '@angular/router';

/** 課程 Course feature routes. Lazily loaded from the root router. */
export const courseRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./course-list/course-list').then((m) => m.CourseList),
    title: '課程 Course',
  },
  {
    path: 'new',
    loadComponent: () => import('./course-form/course-form').then((m) => m.CourseForm),
    title: '新增課程',
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./course-form/course-form').then((m) => m.CourseForm),
    title: '編輯課程',
  },
  {
    path: ':id',
    loadComponent: () => import('./course-detail/course-detail').then((m) => m.CourseDetail),
    title: '檢視課程',
  },
];
