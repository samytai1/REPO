import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '@environments/environment';

import { fakeAccessToken, signIn } from '@core/testing/auth.testing';

import { authTokenInterceptor } from './auth-token.interceptor';

describe('authTokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  const apiUrl = `${environment.apiBaseUrl}/publish-statuses`;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authTokenInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches the session token as an Authorization: Bearer header', () => {
    signIn(['Admin']);
    setup();

    http.get(apiUrl).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${fakeAccessToken(['Admin'])}`);
    request.flush([]);
  });

  it('attaches the header on every verb, not just GET', () => {
    signIn(['Admin']);
    setup();

    http.post(apiUrl, {}).subscribe();
    http.put(apiUrl, {}).subscribe();
    http.delete(`${apiUrl}/1`).subscribe();

    for (const request of httpMock.match(() => true)) {
      expect(request.request.headers.has('Authorization')).toBeTrue();
      request.flush(null);
    }
  });

  it('sends no Authorization header when nobody is signed in', () => {
    setup();

    http.get(apiUrl).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush([]);
  });

  it('picks up a token stored after the interceptor was first used', () => {
    setup();

    http.get(apiUrl).subscribe();
    httpMock.expectOne((r) => !r.headers.has('Authorization')).flush([]);

    signIn(['Admin']);

    http.get(apiUrl).subscribe();
    const second = httpMock.expectOne(apiUrl);
    expect(second.request.headers.get('Authorization')).toBe(`Bearer ${fakeAccessToken(['Admin'])}`);
    second.flush([]);
  });

  it('never leaks the token to a URL outside this API', () => {
    signIn(['Admin']);
    setup();

    http.get('https://example.com/tracking').subscribe();

    const request = httpMock.expectOne('https://example.com/tracking');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });
});
