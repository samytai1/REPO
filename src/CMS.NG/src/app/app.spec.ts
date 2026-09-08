import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';

import { NAV_GROUPS } from '@shared/layout/nav-menu';

import { App } from './app';

describe('App shell', () => {
  /** Every item across every group — the shell renders them all with the groups expanded. */
  const navItemCount = NAV_GROUPS.reduce((total, group) => total + group.items.length, 0);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();
  });

  it('creates the shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with its feature entries', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');
    expect(text).toContain('發布狀態 PublishStatus');
  });

  it('links the PublishStatus menu entry to /publish-statuses', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a.app-nav-item'),
    );

    expect(links.map((link) => link.getAttribute('href'))).toContain('/publish-statuses');
  });

  it('renders the 課程管理 Course group and links Partner to /partners', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('課程管理 Course');
    expect(text).toContain('合作夥伴 Partner');

    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a.app-nav-item'),
    );
    expect(links.map((link) => link.getAttribute('href'))).toContain('/partners');
  });

  it('links the AppRole menu entry to /app-roles', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    // Find the entry by label — the first link belongs to whichever group is listed first.
    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a.app-nav-item'),
    );
    const appRoleLink = links.find((link) => link.textContent?.includes('角色 AppRole'));
    expect(appRoleLink?.getAttribute('href')).toBe('/app-roles');
  });

  it('links the FeaturedPromoItem menu entry, in the 首頁 Home group, to /featured-promo-items', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('首頁 Home');
    expect(fixture.nativeElement.textContent).toContain('上稿作業 FeaturedPromoItem');

    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a.app-nav-item'),
    );
    expect(links.map((link) => link.getAttribute('href'))).toContain('/featured-promo-items');
  });

  it('collapses and expands the sidebar', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const shell = () => fixture.nativeElement.querySelector('.app-shell');
    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('.app-sidebar__toggle');

    expect(shell().classList).not.toContain('app-shell--collapsed');

    toggle.click();
    fixture.detectChanges();
    expect(shell().classList).toContain('app-shell--collapsed');

    toggle.click();
    fixture.detectChanges();
    expect(shell().classList).not.toContain('app-shell--collapsed');
  });

  it('collapses a nav group, hiding only that group items', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const groupHeader: HTMLButtonElement =
      fixture.nativeElement.querySelector('.app-nav-group__header');

    expect(fixture.nativeElement.querySelectorAll('a.app-nav-item').length).toBe(navItemCount);

    groupHeader.click();
    fixture.detectChanges();

    // Only the first group folds away; any later group keeps its entries on screen.
    expect(fixture.nativeElement.querySelectorAll('a.app-nav-item').length).toBe(
      navItemCount - NAV_GROUPS[0].items.length,
    );
  });
});
