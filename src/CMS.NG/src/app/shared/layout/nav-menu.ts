/** A single navigable page inside a nav group. */
export interface NavItem {
  /** Chinese + English label, e.g. `角色 AppRole`. */
  label: string;
  icon: string;
  route: string;
}

/** A collapsible sidebar group, e.g. `系統管理 Admin`. */
export interface NavGroup {
  label: string;
  icon: string;
  /**
   * Roles that may see this group. Omit it — as most groups do — to show it to everyone signed in.
   * Matched against the `role` claims in the access token; the API guards the data either way.
   */
  roles?: string[];
  items: NavItem[];
}

/**
 * Sidebar navigation model. Add a new feature's entry to the group it belongs to —
 * this is the single place the shell reads its menu from.
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    label: '首頁 Home',
    icon: 'pi pi-home',
    items: [
      { label: '上稿作業 FeaturedPromoItem', icon: 'pi pi-megaphone', route: '/featured-promo-items' },
    ],
  },
  {
    label: '系統管理 Admin',
    icon: 'pi pi-shield',
    roles: ['Admin'],
    items: [
      { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
      { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
    ],
  },
  {
    label: '課程管理 Course',
    icon: 'pi pi-book',
    items: [
      { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
      { label: '合作夥伴 Partner', icon: 'pi pi-briefcase', route: '/partners' },
    ],
  },
];
