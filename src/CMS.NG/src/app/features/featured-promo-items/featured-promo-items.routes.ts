import { Routes } from '@angular/router';

/**
 * 上稿作業 FeaturedPromoItem feature routes. Lazily loaded from the root router.
 * The feature is a single weekly grid with inline New / Edit — no detail or form routes.
 */
export const featuredPromoItemRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./featured-promo-item-list/featured-promo-item-list').then(
        (m) => m.FeaturedPromoItemList,
      ),
    title: '上稿作業 FeaturedPromoItem',
  },
];
