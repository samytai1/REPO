import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '@environments/environment';

import { AUTH_SESSION_KEY, LOGIN_ROUTE } from '@core/services/auth.service';
import { signIn } from '@core/testing/auth.testing';

import { authErrorInterceptor } from './auth-error.interceptor';

describe('authErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let navigate: jasmine.Spy;

  const apiUrl = `${environment.apiBaseUrl}/publish-statuses`;
  const loginUrl = `${environment.apiBaseUrl}/auth/login`;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authErrorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    navigate = spyOn(router, 'navigate').and.resolveTo(true);
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('clears the session and redirects to the login page on a 401', () => {
    signIn(['Admin']);
    sessionStorage.setItem('course-list-filters', '{"keyword":"abc"}');
    setup();

    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
    expect(sessionStorage.getItem('course-list-filters')).toBeNull();
    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE], jasmine.anything());
  });

  it('keeps the page the user was on as returnUrl', () => {
    signIn(['Admin']);
    setup();
    spyOnProperty(router, 'url', 'get').and.returnValue('/courses/12/edit');

    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE], {
      queryParams: { returnUrl: '/courses/12/edit' },
    });
  });

  it('does not point returnUrl back at the login page', () => {
    setup();
    spyOnProperty(router, 'url', 'get').and.returnValue('/login?returnUrl=%2Fcourses');

    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE], { queryParams: {} });
  });

  it('re-throws the 401 so the caller still sees it', () => {
    signIn(['Admin']);
    setup();

    let error: unknown;
    http.get(apiUrl).subscribe({ error: (e: unknown) => (error = e) });
    httpMock.expectOne(apiUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(error instanceof HttpErrorResponse).toBeTrue();
    expect((error as HttpErrorResponse).status).toBe(401);
  });

  it('leaves the session alone for a failed login — that 401 means "wrong password"', () => {
    setup();

    http.post(loginUrl, { userId: 'admin', password: 'nope' }).subscribe({ error: () => undefined });
    httpMock.expectOne(loginUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });

  it('ignores other failures', () => {
    signIn(['Admin']);
    setup();

    for (const status of [400, 403, 404, 409, 500]) {
      http.get(apiUrl).subscribe({ error: () => undefined });
      httpMock.expectOne(apiUrl).flush(null, { status, statusText: 'Failed' });
    }

    expect(navigate).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).not.toBeNull();
  });

  it('lets a successful response through untouched', () => {
    signIn(['Admin']);
    setup();

    let body: unknown;
    http.get(apiUrl).subscribe((value) => (body = value));
    httpMock.expectOne(apiUrl).flush([{ pkid: 1 }]);

    expect(body).toEqual([{ pkid: 1 }]);
    expect(navigate).not.toHaveBeenCalled();
  });
});
