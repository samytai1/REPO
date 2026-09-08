import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { environment } from '@environments/environment';

import { FeaturedPromoItem, FeaturedPromoItemClipboard, TrainingCenterLookup } from '@core/models';
import { toIso } from '@core/utils/date.util';
import { FeaturedPromoItemList } from './featured-promo-item-list';

type EditingCell = {
  scheduleOn: string;
  slot: number;
  item: FeaturedPromoItem | null;
  prefill: FeaturedPromoItemClipboard | null;
};

type DaySection = {
  iso: string;
  label: string;
  slots: { slot: number; item: FeaturedPromoItem | null }[];
};

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  trainingCenters: () => TrainingCenterLookup[];
  activeTrainingCenterPkid: () => number | null;
  weekStart: () => Date;
  weekLabel: () => string;
  days: () => DaySection[];
  items: () => FeaturedPromoItem[];
  loading: () => boolean;
  editing: () => EditingCell | null;
  clipboard: () => FeaturedPromoItemClipboard | null;
  selectTab(value: string | number | undefined): void;
  previousWeek(): void;
  nextWeek(): void;
  thisWeek(): void;
  openNew(scheduleOn: string, slot: number): void;
  openEdit(item: FeaturedPromoItem): void;
  openPaste(scheduleOn: string, slot: number): void;
  isEditing(scheduleOn: string, slot: number): boolean;
  onSaved(): void;
  onCancelled(): void;
  copy(item: FeaturedPromoItem): void;
  canMoveUp(item: FeaturedPromoItem | null): boolean;
  canMoveDown(item: FeaturedPromoItem | null): boolean;
  moveUp(item: FeaturedPromoItem): void;
  moveDown(item: FeaturedPromoItem): void;
  confirmDelete(item: FeaturedPromoItem): void;
};

describe('FeaturedPromoItemList', () => {
  const itemsUrl = `${environment.apiBaseUrl}/featured-promo-items`;
  const queryUrl = `${itemsUrl}/query`;
  const centersUrl = `${environment.apiBaseUrl}/lookups/training-centers`;
  const filtersKey = 'featured-promo-item-list-filters';

  const centers: TrainingCenterLookup[] = [
    { pkid: 1, name: '台北', displayOrder: 1 },
    { pkid: 2, name: '新竹', displayOrder: 2 },
  ];

  const base: FeaturedPromoItem = {
    pkid: 0,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    promotion: { pkid: 10, promoCode: '20251204_SkillTrainAI' },
    trainingCenter: { pkid: 1, name: '台北' },
  };

  const monSlot1: FeaturedPromoItem = { ...base, pkid: 1 };
  const monSlot3: FeaturedPromoItem = {
    ...base,
    pkid: 2,
    slot: 3,
    promotionPkid: 12,
    topic: 'n8n自動化三部曲',
    description: '從自動化新手',
    promotion: { pkid: 12, promoCode: '20251215_n8n' },
  };
  const wedSlot2: FeaturedPromoItem = { ...base, pkid: 3, scheduleOn: '2026-03-18', slot: 2 };
  const items = [monSlot1, monSlot3, wedSlot2];

  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;

  function configure(): Promise<void> {
    return TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();
  }

  function create(): void {
    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  /** Answers the training-center lookup and the week query that ngOnInit fires. */
  function flushInitialLoad(payload: FeaturedPromoItem[] = items): void {
    httpMock.expectOne(centersUrl).flush(centers);
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  function expectQuery(): ReturnType<HttpTestingController['expectOne']> {
    return httpMock.expectOne(queryUrl);
  }

  beforeEach(async () => {
    sessionStorage.clear();
    // A Wednesday: proves the restored week snaps to its Monday.
    sessionStorage.setItem(filtersKey, JSON.stringify({ trainingCenterPkid: 1, weekOf: '2026-03-18' }));

    await configure();
    create();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ---------- Loading ----------

  it('loads the training centers, keeps the restored tab and queries its week from Monday', () => {
    httpMock.expectOne(centersUrl).flush(centers);

    const req = expectQuery();
    expect(req.request.body).toEqual({ trainingCenterPkid: 1, weekOf: '2026-03-16' });
    req.flush(items);
    fixture.detectChanges();

    expect(component.trainingCenters()).toEqual(centers);
    expect(component.activeTrainingCenterPkid()).toBe(1);
    expect(component.items().length).toBe(3);
    expect(component.loading()).toBeFalse();
  });

  it('falls back to the first training center when nothing is restored', () => {
    flushInitialLoad();

    sessionStorage.clear();
    const fresh = TestBed.createComponent(FeaturedPromoItemList);
    const freshComponent = fresh.componentInstance as unknown as ListInternals;
    fresh.detectChanges();

    httpMock.expectOne(centersUrl).flush([centers[1], centers[0]]);
    const req = expectQuery();
    expect(req.request.body.trainingCenterPkid).toBe(2);
    req.flush([]);

    expect(freshComponent.activeTrainingCenterPkid()).toBe(2);
  });

  it('defaults to the Monday of the current week when no week is restored', () => {
    flushInitialLoad();

    sessionStorage.clear();
    const fresh = TestBed.createComponent(FeaturedPromoItemList);
    const freshComponent = fresh.componentInstance as unknown as ListInternals;
    fresh.detectChanges();

    httpMock.expectOne(centersUrl).flush(centers);
    expectQuery().flush([]);

    const weekStart = freshComponent.weekStart();
    const today = new Date();
    const daysSinceMonday = (today.getTime() - weekStart.getTime()) / 86_400_000;
    expect(weekStart.getDay()).toBe(1);
    expect(daysSinceMonday).toBeGreaterThanOrEqual(0);
    expect(daysSinceMonday).toBeLessThan(7);
  });

  it('still shows the restored week when the training-center lookup fails', () => {
    httpMock.expectOne(centersUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    expectQuery().flush(items);
    fixture.detectChanges();

    expect(component.items().length).toBe(3);
  });

  it('clears the grid and stops loading when the query fails', () => {
    httpMock.expectOne(centersUrl).flush(centers);
    expectQuery().flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.items()).toEqual([]);
    expect(component.loading()).toBeFalse();
  });

  // ---------- Grid shape ----------

  it('builds seven day sections, Monday to Sunday, each with slots 1–3', () => {
    flushInitialLoad();

    const days = component.days();
    expect(days.map((d) => d.iso)).toEqual([
      '2026-03-16',
      '2026-03-17',
      '2026-03-18',
      '2026-03-19',
      '2026-03-20',
      '2026-03-21',
      '2026-03-22',
    ]);
    expect(days[0].label).toBe('3/16 (一)');
    expect(days[6].label).toBe('3/22 (日)');
    expect(days.every((d) => d.slots.map((s) => s.slot).join() === '1,2,3')).toBeTrue();
    expect(component.weekLabel()).toBe('3/16 -- 3/22');
  });

  it('places each item in its day and slot cell and leaves the rest empty', () => {
    flushInitialLoad();

    const days = component.days();
    expect(days[0].slots[0].item?.pkid).toBe(1);
    expect(days[0].slots[1].item).toBeNull();
    expect(days[0].slots[2].item?.pkid).toBe(2);
    expect(days[2].slots[1].item?.pkid).toBe(3);
    expect(days[6].slots.every((s) => s.item === null)).toBeTrue();
  });

  it('renders the PromoCode, Topic and Description of an occupied slot', () => {
    flushInitialLoad();

    const rows = fixture.nativeElement.querySelectorAll('.featured-promo-item-list__row');
    expect(rows.length).toBe(21);

    const first = rows[0];
    expect(first.querySelector('.featured-promo-item-list__slot').textContent.trim()).toBe('1');
    expect(first.querySelector('.featured-promo-item-list__code').textContent.trim()).toBe('20251204_SkillTrainAI');
    expect(first.querySelector('.featured-promo-item-list__topic').textContent.trim()).toBe('成為能AI協作的程式設計師');
    expect(first.querySelector('.featured-promo-item-list__description').textContent.trim()).toBe('轉職就業養成班');

    const empty = rows[1];
    expect(empty.classList).toContain('featured-promo-item-list__row--empty');
    expect(empty.querySelector('.featured-promo-item-list__code').textContent.trim()).toBe('');
  });

  it('renders one tab per training center', () => {
    flushInitialLoad();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('台北');
    expect(text).toContain('新竹');
  });

  // ---------- Tabs and week navigation ----------

  it('switching the tab reloads the week for that training center and persists it', () => {
    flushInitialLoad();

    component.selectTab(2);

    const req = expectQuery();
    expect(req.request.body).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-16' });
    req.flush([]);

    expect(component.activeTrainingCenterPkid()).toBe(2);
    expect(JSON.parse(sessionStorage.getItem(filtersKey) ?? '{}').trainingCenterPkid).toBe(2);
  });

  it('re-selecting the active tab does not reload', () => {
    flushInitialLoad();

    component.selectTab(1);

    httpMock.expectNone(queryUrl);
    expect(component.activeTrainingCenterPkid()).toBe(1);
  });

  it('moves a week forward and back, seven days at a time, and persists the Monday', () => {
    flushInitialLoad();

    component.nextWeek();
    let req = expectQuery();
    expect(req.request.body.weekOf).toBe('2026-03-23');
    req.flush([]);
    expect(component.weekLabel()).toBe('3/23 -- 3/29');
    expect(JSON.parse(sessionStorage.getItem(filtersKey) ?? '{}').weekOf).toBe('2026-03-23');

    component.previousWeek();
    req = expectQuery();
    expect(req.request.body.weekOf).toBe('2026-03-16');
    req.flush(items);

    component.previousWeek();
    req = expectQuery();
    expect(req.request.body.weekOf).toBe('2026-03-09');
    req.flush([]);
  });

  it('本週 jumps back to the Monday of the current week', () => {
    flushInitialLoad();

    component.thisWeek();

    const req = expectQuery();
    const weekOf = req.request.body.weekOf as string;
    req.flush([]);

    expect(weekOf).toBe(toIso(component.weekStart()) ?? '');
    expect(component.weekStart().getDay()).toBe(1);
  });

  // ---------- Inline form ----------

  it('編輯 on an occupied slot opens the inline form in edit mode for that cell', () => {
    flushInitialLoad();

    component.openEdit(monSlot3);
    fixture.detectChanges();

    expect(component.editing()).toEqual({ scheduleOn: '2026-03-16', slot: 3, item: monSlot3, prefill: null });
    expect(component.isEditing('2026-03-16', 3)).toBeTrue();
    expect(component.isEditing('2026-03-16', 1)).toBeFalse();
    expect(fixture.nativeElement.querySelectorAll('app-featured-promo-item-form').length).toBe(1);
  });

  it('新增 on an empty slot opens the inline form in new mode for that cell', () => {
    flushInitialLoad();

    component.openNew('2026-03-17', 2);

    expect(component.editing()).toEqual({ scheduleOn: '2026-03-17', slot: 2, item: null, prefill: null });
  });

  it('a save closes the form and reloads the week; a cancel just closes it', () => {
    flushInitialLoad();

    component.openNew('2026-03-17', 2);
    component.onSaved();

    expect(component.editing()).toBeNull();
    expectQuery().flush(items);

    component.openNew('2026-03-17', 2);
    component.onCancelled();

    expect(component.editing()).toBeNull();
    httpMock.expectNone(queryUrl);
  });

  it('changing tab or week closes an open form', () => {
    flushInitialLoad();

    component.openNew('2026-03-17', 2);
    component.nextWeek();
    expectQuery().flush([]);

    expect(component.editing()).toBeNull();
  });

  // ---------- Copy / Paste ----------

  it('複製 keeps the promotion and text in the clipboard, and 貼上 seeds a new item with them', () => {
    flushInitialLoad();

    expect(component.clipboard()).toBeNull();

    component.copy(monSlot3);

    const expected: FeaturedPromoItemClipboard = {
      promotionPkid: 12,
      promoCode: '20251215_n8n',
      topic: 'n8n自動化三部曲',
      description: '從自動化新手',
    };
    expect(component.clipboard()).toEqual(expected);
    expect(JSON.parse(sessionStorage.getItem('featured-promo-item-clipboard') ?? '{}')).toEqual(expected);

    component.openPaste('2026-03-19', 1);

    expect(component.editing()).toEqual({ scheduleOn: '2026-03-19', slot: 1, item: null, prefill: expected });
  });

  it('貼上 does nothing while the clipboard is empty', () => {
    flushInitialLoad();

    component.openPaste('2026-03-19', 1);

    expect(component.editing()).toBeNull();
  });

  it('restores the clipboard from session storage', () => {
    flushInitialLoad();

    const stored: FeaturedPromoItemClipboard = {
      promotionPkid: 10,
      promoCode: '20251204_SkillTrainAI',
      topic: 't',
      description: 'd',
    };
    sessionStorage.setItem('featured-promo-item-clipboard', JSON.stringify(stored));

    const fresh = TestBed.createComponent(FeaturedPromoItemList);
    const freshComponent = fresh.componentInstance as unknown as ListInternals;
    fresh.detectChanges();
    httpMock.expectOne(centersUrl).flush(centers);
    expectQuery().flush([]);

    expect(freshComponent.clipboard()).toEqual(stored);
  });

  // ---------- Move slot (+ / −) ----------

  it('only allows − above slot 1 and + below slot 3, never on an empty cell', () => {
    flushInitialLoad();

    expect(component.canMoveUp(monSlot1)).toBeFalse();
    expect(component.canMoveDown(monSlot1)).toBeTrue();
    expect(component.canMoveUp(monSlot3)).toBeTrue();
    expect(component.canMoveDown(monSlot3)).toBeFalse();
    expect(component.canMoveUp(null)).toBeFalse();
    expect(component.canMoveDown(null)).toBeFalse();
  });

  it('+ moves the item one slot down through the move-slot endpoint, then reloads', () => {
    flushInitialLoad();

    component.moveDown(monSlot1);

    const move = httpMock.expectOne(`${itemsUrl}/1/move-slot`);
    expect(move.request.method).toBe('POST');
    expect(move.request.body).toEqual({ targetSlot: 2 });
    move.flush({ ...monSlot1, slot: 2 });

    expectQuery().flush([{ ...monSlot1, slot: 2 }, monSlot3, wedSlot2]);
    expect(component.days()[0].slots[1].item?.pkid).toBe(1);
  });

  it('− moves the item one slot up', () => {
    flushInitialLoad();

    component.moveUp(monSlot3);

    const move = httpMock.expectOne(`${itemsUrl}/2/move-slot`);
    expect(move.request.body).toEqual({ targetSlot: 2 });
    move.flush({ ...monSlot3, slot: 2 });
    expectQuery().flush(items);
  });

  it('ignores − on slot 1 and + on slot 3', () => {
    flushInitialLoad();

    component.moveUp(monSlot1);
    component.moveDown(monSlot3);

    httpMock.expectNone(`${itemsUrl}/1/move-slot`);
    httpMock.expectNone(`${itemsUrl}/2/move-slot`);
    expect(component.items().length).toBe(3);
  });

  it('reports a failed move without reloading', () => {
    flushInitialLoad();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    component.moveDown(monSlot1);
    httpMock.expectOne(`${itemsUrl}/1/move-slot`).flush('boom', { status: 500, statusText: 'Server Error' });

    httpMock.expectNone(queryUrl);
    expect(add.calls.mostRecent().args[0].summary).toBe('移動失敗');
  });

  // ---------- Delete ----------

  it('deletes only after the confirmation is accepted, then reloads', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: Confirmation) => {
      options.accept?.();
      return confirmationService;
    });

    component.confirmDelete(wedSlot2);

    const deleteReq = httpMock.expectOne(`${itemsUrl}/3`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    expectQuery().flush([monSlot1, monSlot3]);
    expect(component.items().length).toBe(2);
  });

  it('names the day, slot and PromoCode in the delete confirmation', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(wedSlot2);

    expect(confirm.calls.mostRecent().args[0].message).toContain('3/18 (三) 版位 2「20251204_SkillTrainAI」');
  });

  it('does not delete when the confirmation is dismissed', () => {
    flushInitialLoad();

    const confirmationService = TestBed.inject(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    component.confirmDelete(wedSlot2);

    httpMock.expectNone(`${itemsUrl}/3`);
    expect(component.items().length).toBe(3);
  });
});
