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
    expect(fixture.nativeElement.querySelectorAll('input').length).toBe(1);
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
