import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';

import { AuthService } from '@core/services';

/**
 * 個人資料 My Profile — the signed-in user's own record.
 *
 * Everything on this page already lives in the session: 帳號 and 使用者名稱 come from the stored
 * profile, and the roles are decoded out of the access token — there is no GET endpoint and no
 * second request, exactly as the sidebar's role gate works.
 *
 * 使用者名稱 is the only editable field. 帳號 and the roles are rendered read-only because the API
 * takes the account from the bearer token and never from the request body: sending them would
 * change nothing, so the form does not offer them.
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

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required, Validators.maxLength(200)]],
  });

  /** 未設定角色 rather than an empty row when the token carries no `role` claim. */
  protected readonly hasRoles = computed(() => this.roles().length > 0);

  protected isInvalid(): boolean {
    const control = this.form.controls.userName;
    return (control.invalid || control.value.trim() === '') && (control.dirty || control.touched);
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
}
