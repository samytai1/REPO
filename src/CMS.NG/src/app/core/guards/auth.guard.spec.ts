import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { AUTH_SESSION_KEY, LOGIN_ROUTE } from '@core/services/auth.service';
import { signIn } from '@core/testing/auth.testing';

import { authGuard } from './auth.guard';

describe('authGuard', () => {
  let router: Router;

  /** Runs the guard in an injection context, as the router does. */
  function run(url: string): boolean | UrlTree {
    const state = { url } as RouterStateSnapshot;

    return TestBed.runInInjectionContext(
      () => authGuard({} as ActivatedRouteSnapshot, state),
    ) as boolean | UrlTree;
  }

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

  it('lets a signed-in user through', () => {
    signIn(['Admin']);
    setup();

    expect(run('/courses')).toBeTrue();
  });

  it('redirects to the login page when there is no token', () => {
    setup();

    const result = run('/courses');

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toContain(LOGIN_ROUTE);
  });

  it('keeps the requested URL as returnUrl', () => {
    setup();

    const result = run('/courses/12/edit');

    expect(router.serializeUrl(result as UrlTree)).toBe(
      `${LOGIN_ROUTE}?returnUrl=%2Fcourses%2F12%2Fedit`,
    );
  });

  it('blocks once the session is cleared, without a reload', () => {
    signIn(['Admin']);
    setup();

    expect(run('/courses')).toBeTrue();

    sessionStorage.clear();

    // The guard reads storage, not a cached signal, so the very next navigation is blocked.
    expect(run('/courses') instanceof UrlTree).toBeTrue();
  });

  it('blocks when the stored session carries no usable token', () => {
    sessionStorage.setItem(
      AUTH_SESSION_KEY,
      JSON.stringify({ userId: 'admin', userName: 'Admin User', accessToken: '' }),
    );
    setup();

    expect(run('/courses') instanceof UrlTree).toBeTrue();
  });
});
