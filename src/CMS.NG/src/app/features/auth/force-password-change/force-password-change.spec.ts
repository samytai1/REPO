import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AUTH_SESSION_KEY, LOGIN_ROUTE } from '@core/services';
import { signIn } from '@core/testing/auth.testing';

import { ForcePasswordChange } from './force-password-change';

describe('ForcePasswordChange page', () => {
  let fixture: ComponentFixture<ForcePasswordChange>;
  let httpMock: HttpTestingController;
  let navigate: jasmine.Spy;
  let messages: MessageService;

  const passwordUrl = `${environment.apiBaseUrl}/auth/password`;

  /**
   * `AuthService` reads session storage in its constructor, so the flagged session has to be
   * written *before* the TestBed hands out an instance.
   */
  function render(): ComponentFixture<ForcePasswordChange> {
    signIn(['Admin'], 'Admin User', true);

    TestBed.configureTestingModule({
      imports: [ForcePasswordChange],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    messages = TestBed.inject(MessageService);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(ForcePasswordChange);
    fixture.detectChanges();
    return fixture;
  }

  /** Fills the three boxes through the real DOM, as a user would. */
  function fillIn(current: string, next: string, confirm: string): void {
    const set = (id: string, value: string) => {
      const input: HTMLInputElement = fixture.nativeElement.querySelector(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };

    set('#currentPassword', current);
    set('#newPassword', next);
    set('#confirmPassword', confirm);

    fixture.detectChanges();
  }

  function submit(): void {
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  const text = (selector: string): string =>
    (fixture.nativeElement.querySelector(selector) as HTMLElement | null)?.textContent?.trim() ?? '';

  /** Flushes a refusal shaped as the API sends one: a 400 with the reason in `detail`. */
  function reject(detail: string): void {
    httpMock
      .expectOne(passwordUrl)
      .flush({ status: 400, title: 'Password rejected', detail }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ---------- Shape ----------

  it('renders three password boxes and explains why', () => {
    render();

    for (const id of ['#currentPassword', '#newPassword', '#confirmPassword']) {
      expect(fixture.nativeElement.querySelector(id)?.getAttribute('type')).toBe('password');
    }

    // No 帳號 box: the account is the token's, exactly as on 個人資料.
    expect(fixture.nativeElement.querySelector('#userId')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('預設密碼');
  });

  // ---------- What is not sent ----------

  it('sends nothing until every box is filled in', () => {
    render();

    submit();

    httpMock.expectNone(passwordUrl);
    expect(fixture.nativeElement.textContent).toContain('目前密碼為必填。');
  });

  it('sends nothing while the two new passwords disagree', () => {
    render();
    fillIn('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rdX');

    submit();

    httpMock.expectNone(passwordUrl);
    expect(text('[data-field="mismatch"]')).toBe('兩次輸入的新密碼不一致。');
  });

  // ---------- What is sent ----------

  it('PUTs the two passwords and nothing else', () => {
    render();
    fillIn('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');

    submit();

    const request = httpMock.expectOne(passwordUrl);
    expect(request.request.method).toBe('PUT');
    // No confirmPassword: re-typing is a UI check, and sending it would put the secret on the wire
    // twice.
    expect(request.request.body).toEqual({
      currentPassword: 'CMS4fun#',
      newPassword: 'N3wP@ssw0rd',
    });
    request.flush(null);
  });

  it('sends the passwords exactly as typed, untrimmed', () => {
    render();
    fillIn('  CMS4fun#  ', '  N3wP@ssw0rd  ', '  N3wP@ssw0rd  ');

    submit();

    const request = httpMock.expectOne(passwordUrl);
    // Trimming would make a legitimate password un-typeable.
    expect(request.request.body).toEqual({
      currentPassword: '  CMS4fun#  ',
      newPassword: '  N3wP@ssw0rd  ',
    });
    request.flush(null);
  });

  // ---------- Success ----------

  it('drops the whole session and returns to the login page on 204', () => {
    render();
    sessionStorage.setItem('course-list-filters', '{"keyword":"abc"}');
    const add = spyOn(messages, 'add');
    fillIn('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');

    submit();
    httpMock.expectOne(passwordUrl).flush(null);
    fixture.detectChanges();

    // The API does not re-issue the token, so the one held still carries the flag — staying signed
    // in would leave the user on a session every other route refuses.
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
    expect(sessionStorage.getItem('course-list-filters')).toBeNull();
    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE]);
    expect(add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'success', detail: '密碼已變更，請重新登入。' }),
    );
  });

  // ---------- Rejection ----------

  it('shows a wrong current password verbatim and keeps the session', () => {
    render();
    fillIn('wrong', 'N3wP@ssw0rd', 'N3wP@ssw0rd');

    submit();
    reject('目前密碼不正確。');

    expect(text('[data-field="password-error"]')).toBe('目前密碼不正確。');
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).not.toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows "same as the current one" verbatim — the message a forced user is likeliest to see', () => {
    render();
    // Retyping the default is the first instinct, and the server is the only thing that knows.
    fillIn('CMS4fun#', 'CMS4fun#', 'CMS4fun#');

    submit();
    reject('新密碼不可與目前密碼相同。');

    expect(text('[data-field="password-error"]')).toBe('新密碼不可與目前密碼相同。');
  });

  it('shows the strength rule the server names', () => {
    render();
    fillIn('CMS4fun#', 'weak', 'weak');

    submit();
    reject('新密碼至少 8 個字元，且需包含大寫字母、小寫字母、數字與符號。');

    expect(text('[data-field="password-error"]')).toContain('新密碼至少 8 個字元');
  });

  it('keeps what was typed so it can be corrected', () => {
    render();
    fillIn('wrong', 'N3wP@ssw0rd', 'N3wP@ssw0rd');

    submit();
    reject('目前密碼不正確。');

    expect((fixture.nativeElement.querySelector('#newPassword') as HTMLInputElement).value).toBe(
      'N3wP@ssw0rd',
    );
  });

  it('falls back to a generic message on a 500', () => {
    render();
    fillIn('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');

    submit();
    httpMock.expectOne(passwordUrl).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text('[data-field="password-error"]')).toBe('密碼更新失敗，請稍後再試。');
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).not.toBeNull();
  });

  it('clears the previous message when the form is submitted again', () => {
    render();
    fillIn('wrong', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submit();
    reject('目前密碼不正確。');
    expect(fixture.nativeElement.querySelector('[data-field="password-error"]')).toBeTruthy();

    fillIn('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submit();

    expect(fixture.nativeElement.querySelector('[data-field="password-error"]')).toBeNull();
    httpMock.expectOne(passwordUrl).flush(null);
  });

  // ---------- The escape hatch ----------

  it('signs out on 登出, because the shell menu is hidden here', () => {
    render();

    const logout: HTMLElement = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).find((button) => (button as HTMLElement).textContent?.includes('登出')) as HTMLElement;

    expect(logout).withContext('the page must offer a way off it').toBeTruthy();

    logout.click();
    fixture.detectChanges();

    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith([LOGIN_ROUTE]);
  });
});
