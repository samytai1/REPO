import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AUTH_SESSION_KEY, DEFAULT_ROUTE } from '@core/services';
import { fakeProfile } from '@core/testing/auth.testing';

import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let httpMock: HttpTestingController;
  let navigateByUrl: jasmine.Spy;

  const loginUrl = `${environment.apiBaseUrl}/auth/login`;

  function setup(queryParams: Record<string, string> = {}): void {
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    navigateByUrl = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
  }

  /** Fills the form through the real DOM, as a user would. */
  function fillIn(userId: string, password: string): void {
    const inputs: Record<string, HTMLInputElement> = {
      userId: fixture.nativeElement.querySelector('#userId'),
      password: fixture.nativeElement.querySelector('#password'),
    };

    inputs['userId'].value = userId;
    inputs['userId'].dispatchEvent(new Event('input'));
    inputs['password'].value = password;
    inputs['password'].dispatchEvent(new Event('input'));

    fixture.detectChanges();
  }

  function submit(): void {
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('renders the sign-in form', () => {
    setup();

    expect(fixture.nativeElement.querySelector('#userId')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#password')?.getAttribute('type')).toBe('password');
    expect(fixture.nativeElement.textContent).toContain('登入');
  });

  it('sends nothing until both fields are filled in', () => {
    setup();

    submit();

    httpMock.expectNone(loginUrl);
    expect(fixture.nativeElement.textContent).toContain('帳號為必填。');
  });

  it('POSTs the trimmed userId and the password exactly as typed', () => {
    setup();
    fillIn('  admin@example.com  ', '  CMS4fun#  ');

    submit();

    const request = httpMock.expectOne(loginUrl);
    expect(request.request.body).toEqual({
      userId: 'admin@example.com',
      // Trimming a password would reject a legitimate one.
      password: '  CMS4fun#  ',
    });
    request.flush(fakeProfile(['Admin']));
  });

  it('stores the profile in session storage and lands on the default page', () => {
    setup();
    fillIn('admin@example.com', 'CMS4fun#');

    submit();
    const profile = fakeProfile(['Admin']);
    httpMock.expectOne(loginUrl).flush(profile);
    fixture.detectChanges();

    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!)).toEqual(profile);
    expect(navigateByUrl).toHaveBeenCalledWith(DEFAULT_ROUTE);
  });

  it('returns to the page the guard bounced the user off', () => {
    setup({ returnUrl: '/courses/12/edit' });
    fillIn('admin@example.com', 'CMS4fun#');

    submit();
    httpMock.expectOne(loginUrl).flush(fakeProfile(['Admin']));

    expect(navigateByUrl).toHaveBeenCalledWith('/courses/12/edit');
  });

  it('ignores a returnUrl that would leave the app or loop back to login', () => {
    for (const returnUrl of ['//evil.example.com', 'https://evil.example.com', '/login']) {
      TestBed.resetTestingModule();
      setup({ returnUrl });
      fillIn('admin@example.com', 'CMS4fun#');

      submit();
      httpMock.expectOne(loginUrl).flush(fakeProfile(['Admin']));

      expect(navigateByUrl).toHaveBeenCalledWith(DEFAULT_ROUTE);
    }
  });

  it('shows the generic failure message on a 401 and stays put', () => {
    setup();
    fillIn('admin@example.com', 'wrong');

    submit();
    httpMock
      .expectOne(loginUrl)
      .flush({ detail: '帳號或密碼錯誤。' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.login__error')?.textContent).toContain(
      '帳號或密碼錯誤。',
    );
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
  });

  it('clears the previous failure when the form is submitted again', () => {
    setup();
    fillIn('admin@example.com', 'wrong');
    submit();
    httpMock.expectOne(loginUrl).flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.login__error')).toBeTruthy();

    fillIn('admin@example.com', 'CMS4fun#');
    submit();

    expect(fixture.nativeElement.querySelector('.login__error')).toBeNull();
    httpMock.expectOne(loginUrl).flush(fakeProfile(['Admin']));
  });
});
