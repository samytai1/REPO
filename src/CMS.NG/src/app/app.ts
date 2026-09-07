import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { NAV_GROUPS, NavGroup } from '@shared/layout/nav-menu';

/**
 * Application shell: collapsible sidebar + routed content area.
 * Toast and ConfirmDialog hosts live here so every feature page can use them.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly brand = 'UWA';
  protected readonly navGroups: NavGroup[] = NAV_GROUPS;

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
}
