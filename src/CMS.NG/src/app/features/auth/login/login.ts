import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';

import { AuthService, DEFAULT_ROUTE, LOGIN_ROUTE } from '@core/services';

/**
 * 登入 Login — the one page outside `authGuard`.
 *
 * On success `AuthService` has already stored the profile in session storage, so the redirect below
 * passes the guard.
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly messageService = inject(MessageService);

  protected readonly submitting = signal(false);

  /** The one generic failure message — the API never says which half was wrong. */
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    password: ['', [Validators.required]],
  });

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { userId, password } = this.form.getRawValue();

    this.errorMessage.set(null);
    this.submitting.set(true);

    // The password travels exactly as typed — trimming it would reject a legitimate one.
    this.auth.login({ userId: userId.trim(), password }).subscribe({
      next: (profile) => {
        this.submitting.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '登入成功',
          detail: `歡迎回來，${profile.userName}。`,
        });
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('帳號或密碼錯誤。');
      },
    });
  }

  /**
   * Where to go once signed in. A `returnUrl` is only honoured when it is a path inside this app —
   * never an absolute URL, and never the login page itself.
   */
  private returnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');

    if (
      requested === null ||
      !requested.startsWith('/') ||
      requested.startsWith('//') ||
      requested.startsWith(LOGIN_ROUTE)
    ) {
      return DEFAULT_ROUTE;
    }

    return requested;
  }
}
