import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';

import { environment } from '@environments/environment';

import { AUTH_SESSION_KEY, AuthService, LOGIN_ROUTE, PROFILE_ROUTE } from '@core/services';
import { signIn } from '@core/testing/auth.testing';
import { NAV_GROUPS } from '@shared/layout/nav-menu';

import { App } from './app';

describe('App shell', () => {
  let currentFixture: ComponentFixture<App> | undefined;

  /** Groups a user with `roles` may see — the shell filters `系統管理 Admin` on the Admin role. */
  const groupsFor = (roles: string[]) =>
    NAV_GROUPS.filter((group) => group.roles === undefined || group.roles.some((r) => roles.includes(r)));

  /** Every item across every visible group — the shell renders them all with the groups expanded. */
  const navItemCount = (roles: string[]) =>
    groupsFor(roles).reduce((total, group) => total + group.items.length, 0);

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    });
  }

  /** Signs the given roles in *before* the shell is built, then renders it. */
  function render(roles: string[] = ['Admin'], userName = 'Admin User'): ComponentFixture<App> {
    signIn(roles, userName);

    configure();
    const fixture = (currentFixture = TestBed.createComponent(App));
    fixture.detectChanges();
    return fixture;
  }

  const navLinks = (fixture: ComponentFixture<App>): HTMLAnchorElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('a.app-nav-item'));

  /**
   * The header user menu is a PrimeNG popup: its entries exist only once the trigger is clicked.
   * It renders inline (no `appendTo`), so the overlay goes away with the shell rather than being
   * left behind in `document.body` for the next spec to find.
   */
  const openUserMenu = (fixture: ComponentFixture<App>): void => {
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('.app-topbar__user');
    trigger.click();
    fixture.detectChanges();
  };

  const menuEntries = (fixture: ComponentFixture<App>): HTMLElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('.app-user-menu .p-menu-item-link'));

  const userMenuLabels = (fixture: ComponentFixture<App>): string[] => {
    openUserMenu(fixture);
    return menuEntries(fixture).map((entry) => entry.textContent?.trim() ?? '');
  };

  beforeEach(() => sessionStorage.clear());
  afterEach(() => {
    // Tear the shell down between specs, so no open popup menu outlives its fixture.
    currentFixture?.destroy();
    currentFixture = undefined;
    sessionStorage.clear();
  });

  it('creates the shell', () => {
    const fixture = render();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with its feature entries', () => {
    const fixture = render(['Admin']);

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');
    expect(text).toContain('發布狀態 PublishStatus');
  });

  it('links the PublishStatus menu entry to /publish-statuses', () => {
    const fixture = render(['Admin']);

    expect(navLinks(fixture).map((link) => link.getAttribute('href'))).toContain('/publish-statuses');
  });

  it('renders the 課程管理 Course group and links Partner to /partners', () => {
    const fixture = render();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('課程管理 Course');
    expect(text).toContain('合作夥伴 Partner');

    expect(navLinks(fixture).map((link) => link.getAttribute('href'))).toContain('/partners');
  });

  it('links the AppRole menu entry to /app-roles', () => {
    const fixture = render(['Admin']);

    // Find the entry by label — the first link belongs to whichever group is listed first.
    const appRoleLink = navLinks(fixture).find((link) => link.textContent?.includes('角色 AppRole'));
    expect(appRoleLink?.getAttribute('href')).toBe('/app-roles');
  });

  it('links the FeaturedPromoItem menu entry, in the 首頁 Home group, to /featured-promo-items', () => {
    const fixture = render();

    expect(fixture.nativeElement.textContent).toContain('首頁 Home');
    expect(fixture.nativeElement.textContent).toContain('上稿作業 FeaturedPromoItem');

    expect(navLinks(fixture).map((link) => link.getAttribute('href'))).toContain(
      '/featured-promo-items',
    );
  });

  it('collapses and expands the sidebar', () => {
    const fixture = render();

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
    const fixture = render(['Admin']);

    const groupHeader: HTMLButtonElement =
      fixture.nativeElement.querySelector('.app-nav-group__header');

    expect(navLinks(fixture).length).toBe(navItemCount(['Admin']));

    groupHeader.click();
    fixture.detectChanges();

    // Only the first group folds away; any later group keeps its entries on screen.
    expect(navLinks(fixture).length).toBe(navItemCount(['Admin']) - NAV_GROUPS[0].items.length);
  });

  // ---------- Role-gated menu ----------

  it('shows 系統管理 Admin only to a user whose roles include Admin', () => {
    const fixture = render(['Admin', 'User']);

    expect(fixture.nativeElement.textContent).toContain('系統管理 Admin');
    expect(navLinks(fixture).map((link) => link.getAttribute('href'))).toContain('/app-roles');
  });

  it('hides 系統管理 Admin — group and links — from a user without the Admin role', () => {
    const fixture = render(['User'], 'Helen Wu');

    const text: string = fixture.nativeElement.textContent;
    expect(text).not.toContain('系統管理 Admin');
    expect(text).not.toContain('角色 AppRole');
    expect(text).not.toContain('發布狀態 PublishStatus');

    const hrefs = navLinks(fixture).map((link) => link.getAttribute('href'));
    expect(hrefs).not.toContain('/app-roles');
    expect(hrefs).not.toContain('/publish-statuses');

    // The rest of the menu is untouched.
    expect(text).toContain('課程管理 Course');
    expect(hrefs).toContain('/courses');
    expect(navLinks(fixture).length).toBe(navItemCount(['User']));
  });

  it('hides 系統管理 Admin from a user carrying no roles at all', () => {
    const fixture = render([], 'Helen Wu');

    expect(fixture.nativeElement.textContent).not.toContain('系統管理 Admin');
    expect(navLinks(fixture).length).toBe(navItemCount([]));
  });

  it('matches the Admin role case-insensitively', () => {
    const fixture = render(['admin']);

    expect(fixture.nativeElement.textContent).toContain('系統管理 Admin');
  });

  // ---------- Signed-in header ----------

  it('shows the signed-in UserName in the header', () => {
    const fixture = render(['Admin'], '王小明');

    expect(fixture.nativeElement.querySelector('.app-topbar__user-name')?.textContent).toContain(
      '王小明',
    );
  });

  it('reflects a rename made on 個人資料 without a reload', () => {
    const fixture = render(['Admin'], 'Admin User');
    const auth = TestBed.inject(AuthService);

    auth.updateUserName('王小明').subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne(`${environment.apiBaseUrl}/auth/profile`)
      .flush({ userId: 'admin@example.com', userName: '王小明' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-topbar__user-name')?.textContent).toContain(
      '王小明',
    );
  });

  it('offers 個人資料 and 登出 from the header user menu', () => {
    const fixture = render(['Admin']);

    expect(userMenuLabels(fixture)).toEqual(['個人資料 My Profile', '登出']);
  });

  it('links 個人資料 to /profile', () => {
    const fixture = render(['Admin']);
    openUserMenu(fixture);

    const link = menuEntries(fixture).find((entry) => entry.textContent?.includes('個人資料'));
    expect(link?.getAttribute('href')).toBe(PROFILE_ROUTE);
  });

  it('clears the session and returns to the login page on 登出', () => {
    const fixture = render(['Admin']);
    sessionStorage.setItem('course-list-filters', '{"keyword":"abc"}');

    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    openUserMenu(fixture);
    const logout = menuEntries(fixture).find((entry) => entry.textContent?.includes('登出'));
    expect(logout).toBeTruthy();

    logout!.click();
    fixture.detectChanges();

    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
    expect(sessionStorage.getItem('course-list-filters')).toBeNull();
    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE]);
    // The chrome goes with the session.
    expect(fixture.nativeElement.querySelector('.app-sidebar')).toBeNull();
    expect(fixture.nativeElement.querySelector('.app-topbar')).toBeNull();
  });

  // ---------- Signed out ----------

  it('renders no sidebar and no header when nobody is signed in', () => {
    configure();
    const fixture = (currentFixture = TestBed.createComponent(App));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-sidebar')).toBeNull();
    expect(fixture.nativeElement.querySelector('.app-topbar')).toBeNull();
    expect(navLinks(fixture).length).toBe(0);
    expect(fixture.nativeElement.querySelector('.app-shell').classList).toContain(
      'app-shell--anonymous',
    );
  });
});
