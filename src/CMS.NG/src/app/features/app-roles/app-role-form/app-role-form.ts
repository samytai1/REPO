import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { forkJoin, of } from 'rxjs';

import { AppRole, AppRoleRequest, AppUserLookup } from '@core/models';
import { AppRoleService, LookupService } from '@core/services';

/** 角色 AppRole — shared add/edit form. Mode is derived from the route. */
@Component({
  selector: 'app-app-role-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
  ],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss',
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  /** Null in add mode; the key of the record being edited otherwise. */
  protected readonly editingRoleId = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly users = signal<AppUserLookup[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required, Validators.maxLength(200)]],
    roleName: ['', [Validators.required, Validators.maxLength(200)]],
    permissionLevel: [100, [Validators.required, Validators.min(0)]],
    description: ['', [Validators.maxLength(400)]],
    userIds: [[] as string[]],
  });

  protected get isEditMode(): boolean {
    return this.editingRoleId() !== null;
  }

  /** `{ label, value }[]` for the n-n multiselect. */
  protected get userOptions(): { label: string; value: string }[] {
    return this.users().map((user) => ({
      label: `${user.userName} (${user.userId})`,
      value: user.userId,
    }));
  }

  ngOnInit(): void {
    const roleId = this.route.snapshot.paramMap.get('id');
    this.editingRoleId.set(roleId);

    // Lookups and the edited record load in parallel.
    forkJoin({
      users: this.lookupService.getAppUsers(),
      role: roleId ? this.service.getById(roleId) : of(null),
    }).subscribe({
      next: ({ users, role }) => {
        this.users.set(users);

        if (role) {
          this.patchFrom(role);
          // The key is immutable once the row exists.
          this.form.controls.roleId.disable();
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得表單資料。',
        });
      },
    });
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled roleId control in edit mode.
    const value = this.form.getRawValue();

    const request: AppRoleRequest = {
      roleId: value.roleId.trim(),
      roleName: value.roleName.trim(),
      permissionLevel: value.permissionLevel,
      description: value.description.trim() || null,
      userIds: value.userIds,
    };

    this.saving.set(true);

    const request$ = this.isEditMode ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEditMode ? '已更新' : '已新增',
          detail: `角色「${saved.roleName}」已儲存。`,
        });
        void this.router.navigate(['/app-roles', saved.roleId]);
      },
      error: (error: { status?: number }) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error?.status === 409 ? '角色代碼已存在，請改用其他代碼。' : '請確認輸入內容後再試一次。',
        });
      },
    });
  }

  protected cancel(): void {
    const roleId = this.editingRoleId();
    void this.router.navigate(roleId ? ['/app-roles', roleId] : ['/app-roles']);
  }

  private patchFrom(role: AppRole): void {
    this.form.patchValue({
      roleId: role.roleId,
      roleName: role.roleName,
      permissionLevel: role.permissionLevel,
      description: role.description ?? '',
      userIds: role.users.map((user) => user.userId),
    });
  }
}
