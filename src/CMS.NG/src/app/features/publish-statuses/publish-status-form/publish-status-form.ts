import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';

import { PublishStatus, PublishStatusRequest } from '@core/models';
import { PublishStatusService } from '@core/services';

/** 發布狀態 PublishStatus — shared add/edit form. Mode is derived from the route. */
@Component({
  selector: 'app-publish-status-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
  ],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss',
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  /** Null in add mode; the key of the record being edited otherwise. */
  protected readonly editingPkid = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    pkid: [0, [Validators.required, Validators.min(0), Validators.max(255)]],
    description: ['', [Validators.required, Validators.maxLength(50)]],
    isDraft: [false],
    isPublished: [false],
    isDiscontinued: [false],
  });

  protected get isEditMode(): boolean {
    return this.editingPkid() !== null;
  }

  ngOnInit(): void {
    const rawPkid = this.route.snapshot.paramMap.get('id');
    const pkid = rawPkid === null ? null : Number(rawPkid);

    // The table has no FKs and no n-n lists, so there is nothing to forkJoin with.
    if (pkid === null || !Number.isInteger(pkid)) {
      this.loading.set(false);
      return;
    }

    this.editingPkid.set(pkid);

    this.service.getById(pkid).subscribe({
      next: (status) => {
        this.patchFrom(status);
        // The key is immutable once the row exists.
        this.form.controls.pkid.disable();
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

    // getRawValue() includes the disabled pkid control in edit mode.
    const value = this.form.getRawValue();

    const request: PublishStatusRequest = {
      pkid: value.pkid,
      description: value.description.trim(),
      isDraft: value.isDraft,
      isPublished: value.isPublished,
      isDiscontinued: value.isDiscontinued,
    };

    this.saving.set(true);

    const request$ = this.isEditMode ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEditMode ? '已更新' : '已新增',
          detail: `發布狀態「${saved.description}」已儲存。`,
        });
        void this.router.navigate(['/publish-statuses', saved.pkid]);
      },
      error: (error: { status?: number }) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error?.status === 409 ? '主代碼已存在，請改用其他代碼。' : '請確認輸入內容後再試一次。',
        });
      },
    });
  }

  protected cancel(): void {
    const pkid = this.editingPkid();
    void this.router.navigate(pkid === null ? ['/publish-statuses'] : ['/publish-statuses', pkid]);
  }

  private patchFrom(status: PublishStatus): void {
    this.form.patchValue({
      pkid: status.pkid,
      description: status.description,
      isDraft: status.isDraft,
      isPublished: status.isPublished,
      isDiscontinued: status.isDiscontinued,
    });
  }
}
