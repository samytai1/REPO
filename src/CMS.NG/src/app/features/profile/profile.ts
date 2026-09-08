import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';

import { AuthService } from '@core/services';
import { problemDetail } from '@core/utils/problem-detail.util';

/**
 * 個人資料 My Profile — the signed-in user's own record, and the two things they may change about
 * it: their 使用者名稱 and their 密碼.
 *
 * Everything shown read-only already lives in the session: 帳號 comes from the stored profile and
 * the roles are decoded out of the access token — there is no GET endpoint and no second request,
 * exactly as the sidebar's role gate works.
 *
 * The two forms are independent. Saving a name never touches the password, and vice versa, so a
 * failure in one leaves the other exactly as the user left it.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, TagModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly userId = this.auth.userId;
  protected readonly roles = this.auth.roles;

  /** The name as the shell currently shows it — it changes the moment a save succeeds. */
  protected readonly userName = this.auth.userName;

  protected readonly saving = signal(false);
  protected readonly changingPassword = signal(false);

  /**
   * The server's reason for refusing a password, shown verbatim. The API owns the strength rules —
   * they are switched on and off by `enforcePasswordPolicy` in SysConfig, which the browser cannot
   * see — so the form checks only "filled in" and "both new entries match" and lets the 400 say the
   * rest. Encoding the rules here as well would let the two drift apart.
   */
  protected readonly passwordError = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required, Validators.maxLength(200)]],
  });

  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: newPasswordsMatch },
  );

  /** 未設定角色 rather than an empty row when the token carries no `role` claim. */
  protected readonly hasRoles = computed(() => this.roles().length > 0);

  protected isInvalid(): boolean {
    const control = this.form.controls.userName;
    return (control.invalid || control.value.trim() === '') && (control.dirty || control.touched);
  }

  protected isPasswordInvalid(controlName: keyof typeof this.passwordForm.controls): boolean {
    const control = this.passwordForm.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  /** True once both new-password boxes have been filled in and they still disagree. */
  protected get passwordsMismatch(): boolean {
    const { newPassword, confirmPassword } = this.passwordForm.controls;

    return (
      this.passwordForm.hasError('passwordMismatch') &&
      confirmPassword.value !== '' &&
      (newPassword.dirty || confirmPassword.dirty || confirmPassword.touched)
    );
  }

  protected save(): void {
    const userName = this.form.controls.userName.value.trim();

    // Trimmed-to-empty is as invalid as empty — the API rejects it too, with the same 400.
    if (this.form.invalid || userName === '') {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.auth.updateUserName(userName).subscribe({
      next: (profile) => {
        this.saving.set(false);
        // Show what was stored, not what was typed, in case SQL had the last word.
        this.form.controls.userName.setValue(profile.userName);
        this.form.markAsPristine();
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: '使用者名稱已更新。',
        });
      },
      error: () => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: '使用者名稱更新失敗，請稍後再試。',
        });
      },
    });
  }

  /** 取消 — drops the edit and shows the name the session still holds. */
  protected reset(): void {
    this.form.reset({ userName: this.auth.userName() });
  }

  protected changePassword(): void {
    this.passwordError.set(null);

    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    // Passwords travel exactly as typed. Trimming would make a legitimate password un-typeable.
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();

    this.changingPassword.set(true);

    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.changingPassword.set(false);
        // The session survives, so there is nothing to re-read — just clear the secrets from the
        // form so they are not left sitting in the DOM.
        this.clearPasswordForm();
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: '密碼已更新。',
        });
      },
      error: (error: unknown) => {
        this.changingPassword.set(false);
        this.passwordError.set(problemDetail(error, '密碼更新失敗，請稍後再試。'));
      },
    });
  }

  /** 取消 on the password card — empties every box and drops any message. */
  protected resetPassword(): void {
    this.passwordError.set(null);
    this.clearPasswordForm();
  }

  private clearPasswordForm(): void {
    this.passwordForm.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
  }
}

/** Cross-field rule: the two new-password boxes must agree before anything is sent. */
function newPasswordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value as string | undefined;
  const confirmPassword = group.get('confirmPassword')?.value as string | undefined;

  return newPassword === confirmPassword ? null : { passwordMismatch: true };
}
