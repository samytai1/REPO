import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';

import { AuthService, LOGIN_ROUTE } from '@core/services';
import { problemDetail } from '@core/utils/problem-detail.util';

/**
 * 變更密碼 — the page a user still on the 預設密碼 is held on.
 *
 * A bare page like 登入, not a mode on 個人資料: the app shell hides its sidebar and header while
 * the session is flagged, because every other route would answer 403 anyway. The server is the
 * control here — `passwordChangeGuard` only spares the user a page full of failed requests.
 *
 * On success the session is dropped and the user signs in again. The API deliberately does **not**
 * re-issue a token, so the one they hold still carries the flag; keeping it would leave them looking
 * at an app that refuses every request.
 */
@Component({
  selector: 'app-force-password-change',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './force-password-change.html',
  styleUrl: './force-password-change.scss',
})
export class ForcePasswordChange {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly submitting = signal(false);

  /**
   * The server's reason for refusing, shown verbatim. The API owns the strength rules, so the form
   * checks only "filled in" and "both new entries match" — and 新密碼不可與目前密碼相同。 is the
   * message a forced user is most likely to see, because retyping the default is the first instinct.
   */
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: newPasswordsMatch },
  );

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  /** True once both new-password boxes have been filled in and they still disagree. */
  protected get passwordsMismatch(): boolean {
    const { newPassword, confirmPassword } = this.form.controls;

    return (
      this.form.hasError('passwordMismatch') &&
      confirmPassword.value !== '' &&
      (newPassword.dirty || confirmPassword.dirty || confirmPassword.touched)
    );
  }

  protected submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // Passwords travel exactly as typed. Trimming would make a legitimate password un-typeable.
    const { currentPassword, newPassword } = this.form.getRawValue();

    this.submitting.set(true);

    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.submitting.set(false);
        this.signOut('密碼已變更', '密碼已變更，請重新登入。');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        // Whatever was typed is kept, so it can be corrected rather than re-entered.
        this.errorMessage.set(problemDetail(error, '密碼更新失敗，請稍後再試。'));
      },
    });
  }

  /**
   * 登出 — the only way off this page without changing anything. The shell's user menu is hidden
   * while the session is flagged, so without it a user would be stuck here.
   */
  protected logout(): void {
    this.signOut();
  }

  /** Clears the session **before** navigating, so no guard can still see the flagged token. */
  private signOut(summary?: string, detail?: string): void {
    this.auth.clearSession();

    if (summary) {
      this.messageService.add({ severity: 'success', summary, detail });
    }

    void this.router.navigate([LOGIN_ROUTE]);
  }
}

/** Cross-field rule: the two new-password boxes must agree before anything is sent. */
function newPasswordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value as string | undefined;
  const confirmPassword = group.get('confirmPassword')?.value as string | undefined;

  return newPassword === confirmPassword ? null : { passwordMismatch: true };
}
