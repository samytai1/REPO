import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { routes } from '../../app.routes';
import {
  DEFAULT_ROUTE,
  FORCE_PASSWORD_CHANGE_ROUTE,
  LOGIN_ROUTE,
} from '@core/services/auth.service';
import { signIn } from '@core/testing/auth.testing';

import { authGuard } from './auth.guard';
import { passwordChangeGuard, unflaggedAwayFromForceGuard } from './password-change.guard';

describe('password-change guards', () => {
  let router: Router;

  /** Runs a guard in an injection context, as the router does. */
  function run(guard: CanActivateFn, url: string): boolean | UrlTree {
    const state = { url } as RouterStateSnapshot;

    return TestBed.runInInjectionContext(
      () => guard({} as ActivatedRouteSnapshot, state),
    ) as boolean | UrlTree;
  }

  const serialized = (result: boolean | UrlTree): string =>
    router.serializeUrl(result as UrlTree);

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    });

    router = TestBed.inject(Router);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  // ---------- passwordChangeGuard ----------

  describe('passwordChangeGuard', () => {
    it('lets an ordinary signed-in user through', () => {
      signIn(['Admin']);
      setup();

      expect(run(passwordChangeGuard, '/courses')).toBeTrue();
    });

    it('redirects a user still on the default password to 變更密碼', () => {
      signIn(['Admin'], 'Admin User', true);
      setup();

      const result = run(passwordChangeGuard, '/courses');

      expect(result instanceof UrlTree).toBeTrue();
      expect(serialized(result)).toBe(FORCE_PASSWORD_CHANGE_ROUTE);
    });

    it('carries no returnUrl, because the forced change ends in a sign-out', () => {
      signIn(['Admin'], 'Admin User', true);
      setup();

      expect(serialized(run(passwordChangeGuard, '/courses/12/edit'))).not.toContain('returnUrl');
    });

    it('reads storage rather than a cached signal', () => {
      signIn(['Admin']);
      setup();

      expect(run(passwordChangeGuard, '/courses')).toBeTrue();

      // Another tab replaces the session with a flagged one — the very next navigation must see it.
      signIn(['Admin'], 'Admin User', true);

      expect(run(passwordChangeGuard, '/courses') instanceof UrlTree).toBeTrue();
    });

    it('lets a signed-out user through, because authGuard owns that case and runs first', () => {
      setup();

      // If this guard redirected too, a signed-out user would land on 變更密碼 instead of /login.
      expect(run(passwordChangeGuard, '/courses')).toBeTrue();
      expect(serialized(run(authGuard, '/courses'))).toContain(LOGIN_ROUTE);
    });
  });

  // ---------- unflaggedAwayFromForceGuard ----------

  describe('unflaggedAwayFromForceGuard', () => {
    it('lets a flagged user onto the page', () => {
      signIn(['Admin'], 'Admin User', true);
      setup();

      expect(run(unflaggedAwayFromForceGuard, FORCE_PASSWORD_CHANGE_ROUTE)).toBeTrue();
    });

    it('sends an ordinary signed-in user back to the default page', () => {
      signIn(['Admin']);
      setup();

      expect(serialized(run(unflaggedAwayFromForceGuard, FORCE_PASSWORD_CHANGE_ROUTE))).toBe(
        DEFAULT_ROUTE,
      );
    });

    it('sends a signed-out user to the login page', () => {
      setup();

      expect(serialized(run(unflaggedAwayFromForceGuard, FORCE_PASSWORD_CHANGE_ROUTE))).toBe(
        LOGIN_ROUTE,
      );
    });
  });

  // ---------- the two together ----------

  it('settles rather than looping for a flagged user', () => {
    signIn(['Admin'], 'Admin User', true);
    setup();

    // passwordChangeGuard sends them to the force route…
    expect(serialized(run(passwordChangeGuard, '/courses'))).toBe(FORCE_PASSWORD_CHANGE_ROUTE);
    // …and the force route accepts them, so the navigation stops there. Giving that route
    // passwordChangeGuard as well would redirect it to itself, and the `**` fallback would turn
    // that into an infinite loop.
    expect(run(unflaggedAwayFromForceGuard, FORCE_PASSWORD_CHANGE_ROUTE)).toBeTrue();
  });

  // ---------- the route table itself ----------

  it('guards every feature route with both guards, and the force route with neither of them', () => {
    // The cheapest insurance there is against the next feature being added with authGuard alone.
    const guardsFor = (path: string) => routes.find((r) => r.path === path)?.canActivate ?? [];

    for (const path of [
      'profile',
      'app-roles',
      'publish-statuses',
      'partners',
      'courses',
      'featured-promo-items',
    ]) {
      expect(guardsFor(path)).toEqual([authGuard, passwordChangeGuard], `route ${path}`);
    }

    expect(guardsFor('change-password')).toEqual([unflaggedAwayFromForceGuard]);
    expect(guardsFor('login')).toEqual([]);
  });
});
