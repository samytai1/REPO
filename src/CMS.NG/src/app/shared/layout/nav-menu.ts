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
  items: NavItem[];
}

/**
 * Sidebar navigation model. Add a new feature's entry to the group it belongs to —
 * this is the single place the shell reads its menu from.
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    label: '系統管理 Admin',
    icon: 'pi pi-shield',
    items: [{ label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' }],
  },
];
