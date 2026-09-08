import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { AuthService, LOGIN_ROUTE, PROFILE_ROUTE } from '@core/services';
import { NAV_GROUPS, NavGroup } from '@shared/layout/nav-menu';

/**
 * Application shell: collapsible sidebar + a header carrying the signed-in user, over the routed
 * content area. Toast and ConfirmDialog hosts live here so every feature page can use them.
 *
 * Signed out — on the login page — the chrome disappears and only the outlet is rendered. The same
 * happens on 變更密碼 while the session is flagged as still using the 預設密碼: every menu entry
 * there leads somewhere the API would answer 403, so none is shown.
 */
@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    MenuModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly brand = 'UUU';

  protected readonly signedIn = computed(() => this.auth.profile() !== null);

  /**
   * Whether to render the sidebar and header. A signed-in user who must still change their 預設密碼
   * gets the same bare page a signed-out one does — kept separate from `signedIn` rather than folded
   * into it, because the two really are different states and `app.spec.ts` asserts on both.
   */
  protected readonly showChrome = computed(() => this.signedIn() && !this.auth.mustChangePassword());

  /** Mirrors the stored profile, so a rename on 個人資料 shows up here without a reload. */
  protected readonly userName = this.auth.userName;

  /**
   * The groups this user may see. A group with no `roles` is for everyone; `系統管理 Admin` lists
   * `Admin`, so it is absent from the menu — not merely disabled — for anyone else.
   */
  protected readonly navGroups = computed<NavGroup[]>(() =>
    NAV_GROUPS.filter((group) => group.roles === undefined || group.roles.some((role) => this.auth.hasRole(role))),
  );

  /**
   * The header's user menu. 個人資料 is a plain route; 登出 is a command, because signing out has to
   * clear the session before the navigation happens.
   */
  protected readonly userMenuItems: MenuItem[] = [
    { label: '個人資料 My Profile', icon: 'pi pi-user-edit', routerLink: PROFILE_ROUTE },
    { separator: true },
    { label: '登出', icon: 'pi pi-sign-out', command: () => this.logout() },
  ];

  protected readonly collapsed = signal(false);

  /** Groups start expanded so the single feature is reachable in one click. */
  protected readonly expandedGroups = signal<Set<string>>(new Set(NAV_GROUPS.map((g) => g.label)));

  protected toggleSidebar(): void {
    this.collapsed.update((value) => !value);
  }

  protected isExpanded(group: NavGroup): boolean {
    return this.expandedGroups().has(group.label);
  }

  protected toggleGroup(group: NavGroup): void {
    this.expandedGroups.update((groups) => {
      const next = new Set(groups);
      if (next.has(group.label)) {
        next.delete(group.label);
      } else {
        next.add(group.label);
      }
      return next;
    });
  }

  /** 登出 — drops the session (profile and every stored list view) and returns to the login page. */
  protected logout(): void {
    this.auth.clearSession();
    void this.router.navigate([LOGIN_ROUTE]);
  }
}
