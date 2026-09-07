import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';

import { Partner, PartnerRequest, PartnerUpdateRequest } from '@core/models';
import { PartnerService } from '@core/services';

/** 合作夥伴 Partner — shared add/edit form. Mode is derived from the route. */
@Component({
  selector: 'app-partner-form',
  imports: [ReactiveFormsModule, ButtonModule, InputNumberModule, InputTextModule],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss',
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  /** Null in add mode; the key of the record being edited otherwise. */
  protected readonly editingPkid = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    pkid: [0],
    name: ['', [Validators.required, Validators.maxLength(50)]],
    appKey: ['', [Validators.required, Validators.maxLength(10)]],
    nameOnPartnerMenu: ['', [Validators.required, Validators.maxLength(200)]],
    nameOnCourseDetailPage: ['', [Validators.required, Validators.maxLength(50)]],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    imageFilename: ['', [Validators.maxLength(50)]],
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
      next: (partner) => {
        this.patchFrom(partner);
        // pkid is an IDENTITY key — shown, never edited.
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
    const imageFilename = value.imageFilename.trim();

    const request: PartnerRequest = {
      name: value.name.trim(),
      appKey: value.appKey.trim(),
      nameOnPartnerMenu: value.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: value.nameOnCourseDetailPage.trim(),
      displayOrder: value.displayOrder,
      // Nullable column: send null, never an empty string.
      imageFilename: imageFilename === '' ? null : imageFilename,
    };

    this.saving.set(true);

    const request$ = this.isEditMode
      ? this.service.update({ ...request, pkid: value.pkid } satisfies PartnerUpdateRequest)
      : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEditMode ? '已更新' : '已新增',
          detail: `合作夥伴「${saved.name}」已儲存。`,
        });
        void this.router.navigate(['/partners', saved.pkid]);
      },
      error: () => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: '請確認輸入內容後再試一次。',
        });
      },
    });
  }

  protected cancel(): void {
    const pkid = this.editingPkid();
    void this.router.navigate(pkid === null ? ['/partners'] : ['/partners', pkid]);
  }

  private patchFrom(partner: Partner): void {
    this.form.patchValue({
      pkid: partner.pkid,
      name: partner.name,
      appKey: partner.appKey,
      nameOnPartnerMenu: partner.nameOnPartnerMenu,
      nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
      displayOrder: partner.displayOrder,
      imageFilename: partner.imageFilename ?? '',
    });
  }
}
