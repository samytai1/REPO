import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@environments/environment';

import { AuthProfile, UserProfile } from '@core/models';
import { fakeAccessToken, fakeProfile, signIn } from '@core/testing/auth.testing';

import { AUTH_SESSION_KEY, AuthService, FORCE_PASSWORD_CHANGE_ROUTE } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const loginUrl = `${environment.apiBaseUrl}/auth/login`;
  const profileUrl = `${environment.apiBaseUrl}/auth/profile`;
  const passwordUrl = `${environment.apiBaseUrl}/auth/password`;

  /** The service reads storage in its constructor, so seed it *before* asking for the instance. */
  function create(): AuthService {
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    return service;
  }

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => {
    httpMock?.verify();
    sessionStorage.clear();
  });

  // ---------- login ----------

  it('POSTs the credentials to /api/auth/login', () => {
    create();
    const profile = fakeProfile(['Admin']);

    let received: AuthProfile | undefined;
    service.login({ userId: 'admin@example.com', password: 'CMS4fun#' }).subscribe((p) => (received = p));

    const request = httpMock.expectOne(loginUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userId: 'admin@example.com', password: 'CMS4fun#' });

    request.flush(profile);
    expect(received).toEqual(profile);
  });

  it('stores the profile in SESSION storage, not local storage', () => {
    create();
    const profile = fakeProfile(['Admin']);

    service.login({ userId: 'admin@example.com', password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(loginUrl).flush(profile);

    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual(profile);
    expect(localStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
  });

  it('exposes the profile, user name and token once signed in', () => {
    create();
    const profile = fakeProfile(['Admin'], '王小明');

    expect(service.profile()).toBeNull();
    expect(service.token()).toBeNull();

    service.login({ userId: 'admin@example.com', password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(loginUrl).flush(profile);

    expect(service.profile()).toEqual(profile);
    expect(service.userName()).toBe('王小明');
    expect(service.token()).toBe(profile.accessToken);
    expect(service.hasToken()).toBeTrue();
  });

  it('leaves the session untouched when the login fails', () => {
    create();

    service.login({ userId: 'admin@example.com', password: 'nope' }).subscribe({ error: () => undefined });
    httpMock.expectOne(loginUrl).flush({ detail: '帳號或密碼錯誤。' }, { status: 401, statusText: 'Unauthorized' });

    expect(service.profile()).toBeNull();
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
  });

  // ---------- restoring a session ----------

  it('restores a session written by an earlier page load', () => {
    const profile = signIn(['Admin', 'User']);
    create();

    expect(service.profile()).toEqual(profile);
    expect(service.roles()).toEqual(['Admin', 'User']);
  });

  it('ignores a corrupt or half-written stored session', () => {
    sessionStorage.setItem(AUTH_SESSION_KEY, '{not json');
    create();
    expect(service.profile()).toBeNull();

    TestBed.resetTestingModule();
    sessionStorage.setItem(AUTH_SESSION_KEY, JSON.stringify({ userId: 'admin', userName: 'Admin' }));
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    create();
    expect(service.profile()).toBeNull();
  });

  it('reads the token from storage on every call, so another tab signing out is seen', () => {
    signIn(['Admin']);
    create();

    expect(service.hasToken()).toBeTrue();

    sessionStorage.clear();

    expect(service.token()).toBeNull();
    expect(service.hasToken()).toBeFalse();
  });

  // ---------- roles ----------

  it('reads the roles from the token, without a second API call', () => {
    signIn(['Admin', 'User']);
    create();

    expect(service.roles()).toEqual(['Admin', 'User']);
    expect(service.hasRole('Admin')).toBeTrue();
    expect(service.hasRole('User')).toBeTrue();

    // Nothing was requested to learn them.
    httpMock.expectNone(() => true);
  });

  it('matches role names case-insensitively', () => {
    signIn(['Admin']);
    create();

    expect(service.hasRole('admin')).toBeTrue();
    expect(service.hasRole('ADMIN')).toBeTrue();
  });

  it('reports no roles for a user who has none', () => {
    signIn([], 'Helen Wu');
    create();

    expect(service.roles()).toEqual([]);
    expect(service.hasRole('Admin')).toBeFalse();
  });

  it('reports no roles when the stored token is unreadable', () => {
    sessionStorage.setItem(
      AUTH_SESSION_KEY,
      JSON.stringify({ userId: 'admin', userName: 'Admin', accessToken: 'garbage' }),
    );
    create();

    expect(service.roles()).toEqual([]);
    expect(service.hasRole('Admin')).toBeFalse();
  });

  // ---------- updateUserName ----------

  it('PUTs only the new name to /api/auth/profile', () => {
    signIn(['Admin'], 'Admin User');
    create();

    let received: UserProfile | undefined;
    service.updateUserName('王小明').subscribe((p) => (received = p));

    const request = httpMock.expectOne(profileUrl);
    expect(request.request.method).toBe('PUT');
    // No userId and no roles: the API takes the account from the token, and a body property is the
    // one thing that could ever override it.
    expect(request.request.body).toEqual({ userName: '王小明' });

    request.flush({ userId: 'admin@example.com', userName: '王小明' });
    expect(received).toEqual({ userId: 'admin@example.com', userName: '王小明' });
  });

  it('refreshes the stored userName — and only the userName — on success', () => {
    const before = signIn(['Admin'], 'Admin User');
    create();

    service.updateUserName('王小明').subscribe();
    httpMock.expectOne(profileUrl).flush({ userId: before.userId, userName: '王小明' });

    expect(service.userName()).toBe('王小明');
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual({
      ...before,
      userName: '王小明',
    });
  });

  it('keeps the token, the userId and the roles across a rename', () => {
    const before = signIn(['Admin', 'User'], 'Admin User');
    create();

    service.updateUserName('王小明').subscribe();
    httpMock.expectOne(profileUrl).flush({ userId: before.userId, userName: '王小明' });

    // The API does not re-issue a token, so nothing the token carries can have changed.
    expect(service.token()).toBe(before.accessToken);
    expect(service.userId()).toBe(before.userId);
    expect(service.roles()).toEqual(['Admin', 'User']);
  });

  it('stores the name the API returned, not the one that was sent', () => {
    signIn(['Admin'], 'Admin User');
    create();

    service.updateUserName('  王小明  ').subscribe();
    httpMock.expectOne(profileUrl).flush({ userId: 'admin@example.com', userName: '王小明' });

    expect(service.userName()).toBe('王小明');
  });

  it('leaves the session untouched when the rename fails', () => {
    const before = signIn(['Admin'], 'Admin User');
    create();

    let failed = false;
    service.updateUserName('').subscribe({ error: () => (failed = true) });
    httpMock.expectOne(profileUrl).flush({ title: 'Bad Request' }, { status: 400, statusText: 'Bad Request' });

    expect(failed).toBeTrue();
    expect(service.userName()).toBe('Admin User');
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual(before);
  });

  // ---------- changePassword ----------

  it('PUTs the two passwords to /api/auth/password', () => {
    signIn(['Admin'], 'Admin User');
    create();

    let completed = false;
    service.changePassword('CMS4fun#', 'N3wP@ssw0rd').subscribe(() => (completed = true));

    const request = httpMock.expectOne(passwordUrl);
    expect(request.request.method).toBe('PUT');
    // No userId — the API takes the account from the token — and no confirm field.
    expect(request.request.body).toEqual({
      currentPassword: 'CMS4fun#',
      newPassword: 'N3wP@ssw0rd',
    });

    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(completed).toBeTrue();
  });

  it('sends passwords exactly as typed, never trimmed', () => {
    signIn(['Admin']);
    create();

    service.changePassword('  spaced pw  ', '  Aa1! pad  ').subscribe();

    const request = httpMock.expectOne(passwordUrl);
    expect(request.request.body).toEqual({
      currentPassword: '  spaced pw  ',
      newPassword: '  Aa1! pad  ',
    });

    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('leaves the session completely alone on success', () => {
    const before = signIn(['Admin', 'User'], 'Admin User');
    create();

    service.changePassword('CMS4fun#', 'N3wP@ssw0rd').subscribe();
    httpMock.expectOne(passwordUrl).flush(null, { status: 204, statusText: 'No Content' });

    // The API keeps the token valid, so there is nothing to re-store and nobody gets signed out.
    expect(service.profile()).toEqual(before);
    expect(service.token()).toBe(before.accessToken);
    expect(service.userName()).toBe('Admin User');
    expect(service.roles()).toEqual(['Admin', 'User']);
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual(before);
  });

  it('passes a rejected password back to the caller as a 400', () => {
    const before = signIn(['Admin']);
    create();

    let status: number | undefined;
    let detail: string | undefined;
    service.changePassword('wrong', 'N3wP@ssw0rd').subscribe({
      error: (error: HttpErrorResponse) => {
        status = error.status;
        detail = (error.error as { detail?: string }).detail;
      },
    });

    httpMock
      .expectOne(passwordUrl)
      .flush({ detail: '目前密碼不正確。' }, { status: 400, statusText: 'Bad Request' });

    // A 400, not a 401 — a 401 would reach authErrorInterceptor and sign the user out over a typo.
    expect(status).toBe(400);
    expect(detail).toBe('目前密碼不正確。');
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual(before);
  });

  // ---------- clearSession ----------

  it('clears the whole session, including stored list state', () => {
    signIn(['Admin']);
    sessionStorage.setItem('course-list-filters', '{"keyword":"abc"}');
    create();

    service.clearSession();

    expect(service.profile()).toBeNull();
    expect(service.hasToken()).toBeFalse();
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
    // A signed-out session must not leave the next user looking at the previous one's filters.
    expect(sessionStorage.getItem('course-list-filters')).toBeNull();
  });

  // ---------- 預設密碼 ----------

  it('reads no default-password flag from an ordinary session', () => {
    signIn(['Admin']);
    create();

    expect(service.mustChangePassword()).toBeFalse();
    expect(service.requiresPasswordChange()).toBeFalse();
  });

  it('reads the flag from a token that carries it', () => {
    signIn(['Admin'], 'Admin User', true);
    create();

    expect(service.mustChangePassword()).toBeTrue();
    expect(service.requiresPasswordChange()).toBeTrue();
  });

  it('is not flagged when nobody is signed in', () => {
    create();

    expect(service.mustChangePassword()).toBeFalse();
    expect(service.requiresPasswordChange()).toBeFalse();
  });

  it('sets both the signal and the storage read when a flagged login is stored', () => {
    create();

    service.login({ userId: 'admin@example.com', password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(loginUrl).flush(fakeProfile(['Admin'], 'Admin User', true));

    expect(service.mustChangePassword()).toBeTrue();
    expect(service.requiresPasswordChange()).toBeTrue();
  });

  it('answers requiresPasswordChange from storage, not from a cached signal', () => {
    signIn(['Admin']);
    create();
    expect(service.requiresPasswordChange()).toBeFalse();

    // Another tab replaces the session. The guard's question must see it without a new instance.
    signIn(['Admin'], 'Admin User', true);

    expect(service.requiresPasswordChange()).toBeTrue();
  });

  it('names the forced page', () => {
    expect(FORCE_PASSWORD_CHANGE_ROUTE).toBe('/change-password');
  });

  it('survives session storage being unavailable', () => {
    create();
    const setItem = spyOn(Storage.prototype, 'setItem').and.throwError('denied');

    service.login({ userId: 'admin@example.com', password: 'CMS4fun#' }).subscribe();
    httpMock.expectOne(loginUrl).flush(fakeProfile(['Admin']));

    // The write threw, but the in-memory session still came up.
    expect(setItem).toHaveBeenCalled();
    expect(service.profile()?.accessToken).toBe(fakeAccessToken(['Admin']));
  });
});
