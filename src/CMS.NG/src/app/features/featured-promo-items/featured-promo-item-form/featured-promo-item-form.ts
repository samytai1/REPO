import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { InputTextModule } from 'primeng/inputtext';

import {
  FeaturedPromoItem,
  FeaturedPromoItemClipboard,
  FeaturedPromoItemRequest,
  FeaturedPromoItemUpdateRequest,
  Promotion2Lookup,
} from '@core/models';
import { FeaturedPromoItemService, LookupService } from '@core/services';

/**
 * 上稿作業 — the inline New / Edit form that opens inside a grid cell.
 *
 * The cell decides the day, training center and slot; the form only edits the promotion (looked
 * up by PromoCode) and the two text columns. Edit mode when `item` is set; a `prefill` (from
 * Copy → Paste) seeds a new item. The form saves itself and reports back through `saved`.
 */
@Component({
  selector: 'app-featured-promo-item-form',
  imports: [ReactiveFormsModule, AutoCompleteModule, ButtonModule, InputTextModule],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss',
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly messageService = inject(MessageService);

  /** ISO `yyyy-MM-dd` of the cell's day. */
  readonly scheduleOn = input.required<string>();
  readonly trainingCenterPkid = input.required<number>();
  readonly slot = input.required<number>();
  /** The item being edited; null in new mode. */
  readonly item = input<FeaturedPromoItem | null>(null);
  /** Copied values to seed a new item with (Paste). Ignored in edit mode. */
  readonly prefill = input<FeaturedPromoItemClipboard | null>(null);

  readonly saved = output<FeaturedPromoItem>();
  readonly cancelled = output<void>();

  protected readonly saving = signal(false);
  protected readonly suggestions = signal<Promotion2Lookup[]>([]);

  protected readonly isEditMode = computed(() => this.item() !== null);

  protected readonly form = this.fb.nonNullable.group({
    promotion: [null as Promotion2Lookup | null, [Validators.required]],
    topic: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.maxLength(300)]],
  });

  ngOnInit(): void {
    const item = this.item();
    const prefill = this.prefill();

    if (item) {
      this.form.patchValue({
        promotion: {
          pkid: item.promotionPkid,
          promoCode: item.promotion?.promoCode ?? String(item.promotionPkid),
          topic: item.topic,
          description: item.description,
        },
        topic: item.topic,
        description: item.description,
      });
    } else if (prefill) {
      this.form.patchValue({
        promotion: {
          pkid: prefill.promotionPkid,
          promoCode: prefill.promoCode,
          topic: prefill.topic,
          description: prefill.description,
        },
        topic: prefill.topic,
        description: prefill.description,
      });
    }
  }

  /** PromoCode prefix search feeding the autocomplete. */
  protected search(event: AutoCompleteCompleteEvent | string): void {
    const query = typeof event === 'string' ? event : event.query;

    this.lookupService.getPromotions(query).subscribe({
      next: (promotions) => this.suggestions.set(promotions),
      error: () => this.suggestions.set([]),
    });
  }

  /**
   * Picking a promotion is a deliberate choice, so its Topic and Description replace whatever is
   * in the two text fields; the user can still edit them afterwards.
   */
  protected onPromotionSelected(event: AutoCompleteSelectEvent | Promotion2Lookup): void {
    const promotion = 'promoCode' in event ? event : (event.value as Promotion2Lookup);

    this.form.patchValue({
      promotion,
      topic: promotion.topic,
      description: promotion.description,
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

    const value = this.form.getRawValue();

    // Validators.required already rejects null; this narrows the type for the request.
    if (!value.promotion) {
      this.form.markAllAsTouched();
      return;
    }

    const request: FeaturedPromoItemRequest = {
      scheduleOn: this.scheduleOn(),
      trainingCenterPkid: this.trainingCenterPkid(),
      slot: this.slot(),
      promotionPkid: value.promotion.pkid,
      topic: value.topic.trim(),
      description: value.description.trim(),
    };

    this.saving.set(true);

    const item = this.item();
    const request$ = item
      ? this.service.update({ ...request, pkid: item.pkid } satisfies FeaturedPromoItemUpdateRequest)
      : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: item ? '已更新' : '已新增',
          detail: `${saved.scheduleOn} 版位 ${saved.slot}「${saved.promotion?.promoCode ?? saved.promotionPkid}」已儲存。`,
        });
        this.saved.emit(saved);
      },
      error: (error: { status?: number }) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error?.status === 409
              ? '此日期、教育中心的版位已有上稿資料。'
              : '請確認輸入內容後再試一次。',
        });
      },
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
