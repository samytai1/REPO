import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { AUTH_SESSION_KEY, AuthService } from '@core/services';
import { signIn } from '@core/testing/auth.testing';

import { Profile } from './profile';

describe('Profile page', () => {
  let fixture: ComponentFixture<Profile>;
  let httpMock: HttpTestingController;

  const profileUrl = `${environment.apiBaseUrl}/auth/profile`;
  const passwordUrl = `${environment.apiBaseUrl}/auth/password`;

  /**
   * `AuthService` reads session storage in its constructor, so the session has to be written
   * *before* the TestBed hands out an instance.
   */
  function render(roles: string[] = ['Admin', 'User'], userName = 'Admin User'): ComponentFixture<Profile> {
    signIn(roles, userName);

    TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        MessageService,
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    return fixture;
  }

  const text = (selector: string): string =>
    (fixture.nativeElement.querySelector(selector) as HTMLElement | null)?.textContent?.trim() ?? '';

  const nameInput = (): HTMLInputElement => fixture.nativeElement.querySelector('#userName');

  const submit = (): void => {
    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  /** Types into the reactive control the way a user would, so the form goes dirty. */
  const type = (value: string): void => {
    const input = nameInput();
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock?.verify();
    sessionStorage.clear();
  });

  // ---------- Read-only fields ----------

  it('shows the signed-in UserId, read-only', () => {
    render();

    expect(text('[data-field="userId"]')).toBe('admin@example.com');
    // Read-only means read-only: there is no control for it, not merely a disabled one.
    expect(fixture.nativeElement.querySelector('#userId')).toBeNull();
    // One text box for the name, plus the three password boxes — and none for 帳號.
    expect(fixture.nativeElement.querySelectorAll('input[type="text"]').length).toBe(1);
  });

  it('shows the roles from the token, read-only, without an API call', () => {
    render(['Admin', 'User']);

    const roles = text('[data-field="roles"]');
    expect(roles).toContain('Admin');
    expect(roles).toContain('User');

    // The roles ride in the token; nothing is fetched for them.
    httpMock.expectNone(profileUrl);
    expect(fixture.nativeElement.querySelector('[data-field="roles"] input')).toBeNull();
  });

  it('shows a placeholder for a user carrying no roles', () => {
    render([], 'Helen Wu');

    expect(text('[data-field="roles"]')).toBe('未設定角色');
  });

  it('pre-fills the editable UserName from the session', () => {
    render(['Admin'], '王小明');

    expect(nameInput().value).toBe('王小明');
  });

  // ---------- Saving ----------

  it('PUTs only the trimmed userName and updates the shell', () => {
    render(['Admin'], 'Admin User');
    const auth = TestBed.inject(AuthService);

    type('  王小明  ');
    submit();

    const request = httpMock.expectOne(profileUrl);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ userName: '王小明' });

    request.flush({ userId: 'admin@example.com', userName: '王小明' });
    fixture.detectChanges();

    // The shell reads `AuthService.userName`, and session storage is what survives a reload.
    expect(auth.userName()).toBe('王小明');
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!).userName).toBe('王小明');
    expect(nameInput().value).toBe('王小明');
  });

  it('leaves the UserId and the roles alone across a save', () => {
    render(['Admin', 'User'], 'Admin User');
    const auth = TestBed.inject(AuthService);
    const tokenBefore = auth.token();

    type('王小明');
    submit();
    httpMock.expectOne(profileUrl).flush({ userId: 'admin@example.com', userName: '王小明' });
    fixture.detectChanges();

    expect(auth.userId()).toBe('admin@example.com');
    expect(auth.roles()).toEqual(['Admin', 'User']);
    expect(auth.token()).toBe(tokenBefore);
    expect(text('[data-field="userId"]')).toBe('admin@example.com');
  });

  it('shows a success toast once the name is stored', () => {
    render();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    type('王小明');
    submit();
    httpMock.expectOne(profileUrl).flush({ userId: 'admin@example.com', userName: '王小明' });
    fixture.detectChanges();

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  // ---------- Validation ----------

  it('refuses to send an empty userName and shows the required message', () => {
    render(['Admin'], 'Admin User');
    const auth = TestBed.inject(AuthService);

    type('');
    submit();

    httpMock.expectNone(profileUrl);
    expect(text('.field-error')).toBe('使用者名稱為必填。');
    expect(auth.userName()).toBe('Admin User');
  });

  it('refuses to send a whitespace-only userName', () => {
    render(['Admin'], 'Admin User');
    const auth = TestBed.inject(AuthService);

    type('    ');
    submit();

    httpMock.expectNone(profileUrl);
    expect(text('.field-error')).toBe('使用者名稱為必填。');
    expect(auth.userName()).toBe('Admin User');
  });

  // ---------- 變更密碼 ----------

  const passwordInput = (id: string): HTMLInputElement =>
    fixture.nativeElement.querySelector(`#${id}`);

  /** Fills all three password boxes the way a user would, so the controls go dirty. */
  const typePasswords = (current: string, next: string, confirm: string): void => {
    for (const [id, value] of [
      ['currentPassword', current],
      ['newPassword', next],
      ['confirmPassword', confirm],
    ] as const) {
      const input = passwordInput(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
  };

  const submitPasswordForm = (): void => {
    const form: HTMLFormElement = fixture.nativeElement.querySelectorAll('form')[1];
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('renders three password boxes, all of type password', () => {
    render();

    for (const id of ['currentPassword', 'newPassword', 'confirmPassword']) {
      expect(passwordInput(id)).withContext(id).toBeTruthy();
      expect(passwordInput(id).type).withContext(id).toBe('password');
    }
  });

  it('states the policy before the fields', () => {
    render();

    expect(text('.profile__hint--policy')).toBe(
      '新密碼至少 8 個字元，且需包含大寫字母、小寫字母、數字與符號。',
    );
  });

  it('PUTs the two passwords, untrimmed, and clears the boxes on success', () => {
    render();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    typePasswords('CMS4fun#', '  Aa1! pad  ', '  Aa1! pad  ');
    submitPasswordForm();

    const request = httpMock.expectOne(passwordUrl);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      currentPassword: 'CMS4fun#',
      newPassword: '  Aa1! pad  ',
    });

    request.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
    // The secrets must not be left sitting in the DOM once they are stored.
    expect(passwordInput('currentPassword').value).toBe('');
    expect(passwordInput('newPassword').value).toBe('');
    expect(passwordInput('confirmPassword').value).toBe('');
  });

  it('keeps the user signed in after a password change', () => {
    render(['Admin', 'User'], 'Admin User');
    const auth = TestBed.inject(AuthService);
    const tokenBefore = auth.token();

    typePasswords('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();
    httpMock.expectOne(passwordUrl).flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(auth.token()).toBe(tokenBefore);
    expect(auth.userName()).toBe('Admin User');
    expect(auth.roles()).toEqual(['Admin', 'User']);
    expect(sessionStorage.getItem(AUTH_SESSION_KEY)).not.toBeNull();
  });

  it('refuses to send when the two new passwords disagree', () => {
    render();

    typePasswords('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rdTypo');
    submitPasswordForm();

    httpMock.expectNone(passwordUrl);
    expect(text('[data-field="mismatch"]')).toBe('兩次輸入的新密碼不一致。');
  });

  it('refuses to send with any box left empty', () => {
    render();

    typePasswords('', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();

    httpMock.expectNone(passwordUrl);
    expect(text('.field-error')).toBeTruthy();
  });

  it('shows the API reason for a rejected password, verbatim', () => {
    render();

    typePasswords('wrong-password', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();

    httpMock
      .expectOne(passwordUrl)
      .flush({ detail: '目前密碼不正確。' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    // The server owns the rules — the browser cannot see enforcePasswordPolicy — so it shows the
    // server's own text rather than a message of its own.
    expect(text('[data-field="password-error"]')).toBe('目前密碼不正確。');
  });

  it('shows the strength rule the API sends back', () => {
    render();

    typePasswords('CMS4fun#', 'weak', 'weak');
    submitPasswordForm();

    httpMock.expectOne(passwordUrl).flush(
      { detail: '新密碼至少 8 個字元，且需包含大寫字母、小寫字母、數字與符號。' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(text('[data-field="password-error"]')).toContain('新密碼至少 8 個字元');
  });

  it('keeps what was typed when the change is refused, so it can be corrected', () => {
    render();

    typePasswords('wrong-password', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();
    httpMock
      .expectOne(passwordUrl)
      .flush({ detail: '目前密碼不正確。' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(passwordInput('newPassword').value).toBe('N3wP@ssw0rd');
  });

  it('falls back to a generic message when the failure carries no detail', () => {
    render();

    typePasswords('CMS4fun#', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();

    httpMock
      .expectOne(passwordUrl)
      .flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(text('[data-field="password-error"]')).toBe('密碼更新失敗，請稍後再試。');
  });

  it('empties the boxes and drops the message on 取消', () => {
    render();

    typePasswords('wrong-password', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();
    httpMock
      .expectOne(passwordUrl)
      .flush({ detail: '目前密碼不正確。' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    const cancel = Array.from(
      fixture.nativeElement.querySelectorAll('.profile__password button'),
    ).find((button) => (button as HTMLElement).textContent?.includes('取消')) as HTMLButtonElement;

    cancel.click();
    fixture.detectChanges();

    expect(passwordInput('currentPassword').value).toBe('');
    expect(fixture.nativeElement.querySelector('[data-field="password-error"]')).toBeNull();
  });

  it('leaves the userName form alone when a password change fails', () => {
    render(['Admin'], 'Admin User');

    type('王小明');
    typePasswords('wrong-password', 'N3wP@ssw0rd', 'N3wP@ssw0rd');
    submitPasswordForm();
    httpMock
      .expectOne(passwordUrl)
      .flush({ detail: '目前密碼不正確。' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    // The two forms are independent: a failure in one must not discard the other.
    expect(nameInput().value).toBe('王小明');
  });

  it('sends no password request when only the name is saved', () => {
    render();

    type('王小明');
    submit();

    httpMock.expectNone(passwordUrl);
    httpMock.expectOne(profileUrl).flush({ userId: 'admin@example.com', userName: '王小明' });
    expect(nameInput().value).toBe('王小明');
  });

  // ---------- Failure and cancel ----------

  it('keeps the stored name and warns when the save fails', () => {
    render(['Admin'], 'Admin User');
    const auth = TestBed.inject(AuthService);
    const add = spyOn(TestBed.inject(MessageService), 'add');

    type('王小明');
    submit();
    httpMock
      .expectOne(profileUrl)
      .flush({ title: 'Bad Request' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
    expect(auth.userName()).toBe('Admin User');
    expect(JSON.parse(sessionStorage.getItem(AUTH_SESSION_KEY)!).userName).toBe('Admin User');
  });

  it('restores the session name on 取消', () => {
    render(['Admin'], 'Admin User');

    type('typed but not saved');

    const cancel: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).find((button) => (button as HTMLElement).textContent?.includes('取消')) as HTMLButtonElement;

    cancel.click();
    fixture.detectChanges();

    expect(nameInput().value).toBe('Admin User');
    httpMock.expectNone(profileUrl);
  });
});
