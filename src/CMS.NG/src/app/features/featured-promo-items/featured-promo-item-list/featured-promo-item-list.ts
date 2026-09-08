import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TabsModule } from 'primeng/tabs';
import { TooltipModule } from 'primeng/tooltip';

import {
  FEATURED_PROMO_SLOTS,
  FeaturedPromoItem,
  FeaturedPromoItemClipboard,
  TrainingCenterLookup,
} from '@core/models';
import { FeaturedPromoItemService, ListStateService, LookupService } from '@core/services';
import { addDays, fromIso, mondayOf, toIso, weekdayLabel } from '@core/utils/date.util';
import { FeaturedPromoItemForm } from '../featured-promo-item-form/featured-promo-item-form';

/** Persisted grid state — the active tab and the week being viewed. */
const FILTERS_KEY = 'featured-promo-item-list-filters';
/** The Copy buffer, kept across navigation so Paste works after a detour. */
const CLIPBOARD_KEY = 'featured-promo-item-clipboard';

interface GridFilters {
  trainingCenterPkid: number | null;
  /** ISO Monday */
  weekOf: string | null;
}

/** One slot row of a day section: the slot number and whatever occupies it. */
interface SlotCell {
  slot: number;
  item: FeaturedPromoItem | null;
}

/** One day section of the week. */
interface DaySection {
  iso: string;
  /** `3/16 (一)` */
  label: string;
  slots: SlotCell[];
}

/** The cell whose inline form is open. */
interface EditingCell {
  scheduleOn: string;
  slot: number;
  item: FeaturedPromoItem | null;
  prefill: FeaturedPromoItemClipboard | null;
}

/**
 * 上稿作業 FeaturedPromoItem — the weekly grid: one tab per training center, a Monday-to-Sunday
 * week navigator, seven day sections of three slots, and inline New / Edit / Copy / Paste /
 * Delete / move-slot actions on each slot.
 */
@Component({
  selector: 'app-featured-promo-item-list',
  imports: [ButtonModule, TabsModule, TooltipModule, FeaturedPromoItemForm],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss',
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly listState = inject(ListStateService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly activeTrainingCenterPkid = signal<number | null>(null);
  /** Local-midnight Monday of the week on screen. */
  protected readonly weekStart = signal<Date>(mondayOf(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly editing = signal<EditingCell | null>(null);
  protected readonly clipboard = signal<FeaturedPromoItemClipboard | null>(null);

  protected readonly slots = FEATURED_PROMO_SLOTS;

  protected readonly weekEnd = computed(() => addDays(this.weekStart(), 6));

  /** `3/16 -- 3/22`, as the navigator shows it. */
  protected readonly weekLabel = computed(
    () => `${shortDate(this.weekStart())} -- ${shortDate(this.weekEnd())}`,
  );

  /** Seven day sections, each with every slot — occupied or not — so empty cells still render. */
  protected readonly days = computed<DaySection[]>(() => {
    const byCell = new Map<string, FeaturedPromoItem>();
    for (const item of this.items()) {
      byCell.set(`${item.scheduleOn}|${item.slot}`, item);
    }

    return Array.from({ length: 7 }, (_, offset) => {
      const date = addDays(this.weekStart(), offset);
      const iso = toIso(date) ?? '';
      return {
        iso,
        label: `${shortDate(date)} (${weekdayLabel(date)})`,
        slots: this.slots.map((slot) => ({ slot, item: byCell.get(`${iso}|${slot}`) ?? null })),
      };
    });
  });

  ngOnInit(): void {
    const saved = this.listState.read<GridFilters>(FILTERS_KEY);
    const savedWeek = fromIso(saved?.weekOf);

    if (saved?.trainingCenterPkid != null) this.activeTrainingCenterPkid.set(saved.trainingCenterPkid);
    if (savedWeek) this.weekStart.set(mondayOf(savedWeek));

    this.clipboard.set(this.listState.read<FeaturedPromoItemClipboard>(CLIPBOARD_KEY));

    this.lookupService.getTrainingCenters().subscribe({
      next: (centers) => {
        this.trainingCenters.set(centers);

        const active = this.activeTrainingCenterPkid();
        if (active === null || !centers.some((c) => c.pkid === active)) {
          this.activeTrainingCenterPkid.set(centers[0]?.pkid ?? null);
        }

        this.load();
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得教育中心清單。',
        });
        // A restored tab can still show its week.
        this.load();
      },
    });
  }

  protected load(): void {
    const trainingCenterPkid = this.activeTrainingCenterPkid();
    if (trainingCenterPkid === null) return;

    const weekOf = toIso(this.weekStart());
    this.listState.write(FILTERS_KEY, { trainingCenterPkid, weekOf } satisfies GridFilters);

    this.loading.set(true);

    this.service.query({ trainingCenterPkid, weekOf }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得上稿資料。',
        });
      },
    });
  }

  // ---------- Tabs and week navigation ----------

  protected selectTab(value: string | number | undefined): void {
    const pkid = Number(value);
    if (!Number.isInteger(pkid) || pkid === this.activeTrainingCenterPkid()) return;

    this.activeTrainingCenterPkid.set(pkid);
    this.editing.set(null);
    this.load();
  }

  protected previousWeek(): void {
    this.goToWeek(addDays(this.weekStart(), -7));
  }

  protected nextWeek(): void {
    this.goToWeek(addDays(this.weekStart(), 7));
  }

  protected thisWeek(): void {
    this.goToWeek(mondayOf(new Date()));
  }

  private goToWeek(monday: Date): void {
    this.weekStart.set(monday);
    this.editing.set(null);
    this.load();
  }

  // ---------- Inline form ----------

  protected openNew(scheduleOn: string, slot: number): void {
    this.editing.set({ scheduleOn, slot, item: null, prefill: null });
  }

  protected openEdit(item: FeaturedPromoItem): void {
    this.editing.set({ scheduleOn: item.scheduleOn, slot: item.slot, item, prefill: null });
  }

  protected openPaste(scheduleOn: string, slot: number): void {
    const prefill = this.clipboard();
    if (!prefill) return;

    this.editing.set({ scheduleOn, slot, item: null, prefill });
  }

  protected isEditing(scheduleOn: string, slot: number): boolean {
    const editing = this.editing();
    return editing !== null && editing.scheduleOn === scheduleOn && editing.slot === slot;
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.load();
  }

  protected onCancelled(): void {
    this.editing.set(null);
  }

  // ---------- Copy / Paste ----------

  protected copy(item: FeaturedPromoItem): void {
    const clipboard: FeaturedPromoItemClipboard = {
      promotionPkid: item.promotionPkid,
      promoCode: item.promotion?.promoCode ?? String(item.promotionPkid),
      topic: item.topic,
      description: item.description,
    };

    this.clipboard.set(clipboard);
    this.listState.write(CLIPBOARD_KEY, clipboard);
    this.messageService.add({
      severity: 'info',
      summary: '已複製',
      detail: `「${clipboard.promoCode}」可貼上到空的版位。`,
    });
  }

  // ---------- Move slot ----------

  /** − : towards slot 1. */
  protected canMoveUp(item: FeaturedPromoItem | null): boolean {
    return item !== null && item.slot > this.slots[0];
  }

  /** + : towards slot 3. */
  protected canMoveDown(item: FeaturedPromoItem | null): boolean {
    return item !== null && item.slot < this.slots[this.slots.length - 1];
  }

  protected moveUp(item: FeaturedPromoItem): void {
    if (this.canMoveUp(item)) this.move(item, item.slot - 1);
  }

  protected moveDown(item: FeaturedPromoItem): void {
    if (this.canMoveDown(item)) this.move(item, item.slot + 1);
  }

  private move(item: FeaturedPromoItem, targetSlot: number): void {
    this.editing.set(null);

    this.service.moveSlot(item.pkid, targetSlot).subscribe({
      next: () => this.load(),
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '移動失敗',
          detail: '請稍後再試一次。',
        }),
    });
  }

  // ---------- Delete ----------

  protected confirmDelete(item: FeaturedPromoItem): void {
    const date = fromIso(item.scheduleOn);
    const when = date ? `${shortDate(date)} (${weekdayLabel(date)})` : item.scheduleOn;

    this.confirmationService.confirm({
      header: '刪除上稿資料',
      message: `確定要刪除 ${when} 版位 ${item.slot}「${item.promotion?.promoCode ?? item.promotionPkid}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(item),
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: `版位 ${item.slot}「${item.promotion?.promoCode ?? item.promotionPkid}」已刪除。`,
        });
        this.editing.set(null);
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: '請稍後再試一次。',
        }),
    });
  }
}

/** `3/16` — month/day without padding, as the grid headers show it. */
function shortDate(date: Date): string {
  return `${date.getMonth() + 1}/${date.getDate()}`;
}
