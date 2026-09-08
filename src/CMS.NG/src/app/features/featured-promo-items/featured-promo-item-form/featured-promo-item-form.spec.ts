import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { FeaturedPromoItem, FeaturedPromoItemClipboard, Promotion2Lookup } from '@core/models';
import { FeaturedPromoItemForm } from './featured-promo-item-form';

type FormInternals = {
  form: FormGroup;
  saving: () => boolean;
  suggestions: () => Promotion2Lookup[];
  isEditMode: () => boolean;
  search(query: string): void;
  onPromotionSelected(promotion: Promotion2Lookup): void;
  save(): void;
  cancel(): void;
};

describe('FeaturedPromoItemForm', () => {
  const itemsUrl = `${environment.apiBaseUrl}/featured-promo-items`;
  const promotionsUrl = `${environment.apiBaseUrl}/lookups/promotions`;

  const n8n: Promotion2Lookup = {
    pkid: 12,
    promoCode: '20251215_n8n',
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師',
  };

  const existing: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 2,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    promotion: { pkid: 10, promoCode: '20251204_SkillTrainAI' },
    trainingCenter: { pkid: 1, name: '台北' },
  };

  const clipboard: FeaturedPromoItemClipboard = {
    promotionPkid: 11,
    promoCode: '251211_GoogleAI',
    topic: 'Google AI工具一次掌握',
    description: '不需技術基礎',
  };

  let fixture: ComponentFixture<FeaturedPromoItemForm>;
  let component: FormInternals;
  let httpMock: HttpTestingController;
  let savedItems: FeaturedPromoItem[];
  let cancelledCount: number;

  async function setup(options: {
    item?: FeaturedPromoItem | null;
    prefill?: FeaturedPromoItemClipboard | null;
  } = {}): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemForm],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), MessageService],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemForm);
    fixture.componentRef.setInput('scheduleOn', '2026-03-16');
    fixture.componentRef.setInput('trainingCenterPkid', 1);
    fixture.componentRef.setInput('slot', 2);
    fixture.componentRef.setInput('item', options.item ?? null);
    fixture.componentRef.setInput('prefill', options.prefill ?? null);

    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);

    savedItems = [];
    cancelledCount = 0;
    fixture.componentInstance.saved.subscribe((item) => savedItems.push(item));
    fixture.componentInstance.cancelled.subscribe(() => cancelledCount++);

    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- New mode ----------

  it('starts empty in new mode and shows the cell it is editing', async () => {
    await setup();

    expect(component.isEditMode()).toBeFalse();
    expect(component.form.getRawValue()).toEqual({ promotion: null, topic: '', description: '' });
    expect(fixture.nativeElement.textContent).toContain('新增');
    expect(fixture.nativeElement.textContent).toContain('2026-03-16');
    expect(fixture.nativeElement.textContent).toContain('版位 2');
  });

  it('search() looks the PromoCode prefix up and stores the suggestions', async () => {
    await setup();

    component.search('2025');

    const req = httpMock.expectOne((r) => r.url === promotionsUrl && r.params.get('keyword') === '2025');
    expect(req.request.method).toBe('GET');
    req.flush([n8n]);

    expect(component.suggestions()).toEqual([n8n]);
  });

  it('search() clears the suggestions when the lookup fails', async () => {
    await setup();
    component.search('2025');
    httpMock.expectOne((r) => r.url === promotionsUrl).flush([n8n]);

    component.search('zzz');
    httpMock.expectOne((r) => r.url === promotionsUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(component.suggestions()).toEqual([]);
  });

  it('selecting a promotion sets Promotion_pkid and pre-fills Topic and Description', async () => {
    await setup();

    component.onPromotionSelected(n8n);

    expect(component.form.getRawValue()).toEqual({
      promotion: n8n,
      topic: 'n8n自動化三部曲',
      description: '從自動化新手到企業級AI架構師',
    });
  });

  it('does not submit until a promotion has been selected', async () => {
    await setup();

    component.form.patchValue({ topic: '主題', description: '說明' });
    component.save();

    httpMock.expectNone(itemsUrl);
    expect(component.form.controls['promotion'].touched).toBeTrue();
    expect(savedItems).toEqual([]);
  });

  it('POSTs the cell coordinates, the looked-up Promotion_pkid and trimmed text, then emits saved', async () => {
    await setup();

    component.onPromotionSelected(n8n);
    component.form.patchValue({ topic: '  n8n 自動化  ', description: '  三部曲  ' });
    component.save();

    const req = httpMock.expectOne(itemsUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      scheduleOn: '2026-03-16',
      trainingCenterPkid: 1,
      slot: 2,
      promotionPkid: 12,
      topic: 'n8n 自動化',
      description: '三部曲',
    });
    expect(req.request.body.pkid).toBeUndefined();

    const created = { ...existing, pkid: 8, promotionPkid: 12, promotion: { pkid: 12, promoCode: n8n.promoCode } };
    req.flush(created);

    expect(component.saving()).toBeFalse();
    expect(savedItems).toEqual([created]);
  });

  it('reports an occupied slot (409) and keeps the form open', async () => {
    await setup();
    const add = spyOn(TestBed.inject(MessageService), 'add');

    component.onPromotionSelected(n8n);
    component.save();

    httpMock.expectOne(itemsUrl).flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(component.saving()).toBeFalse();
    expect(savedItems).toEqual([]);
    expect(add.calls.mostRecent().args[0].detail).toContain('已有上稿資料');
  });

  // ---------- Paste (prefill) ----------

  it('seeds a new item from the clipboard when pasting', async () => {
    await setup({ prefill: clipboard });

    expect(component.isEditMode()).toBeFalse();
    expect(component.form.getRawValue()).toEqual({
      promotion: { pkid: 11, promoCode: '251211_GoogleAI', topic: clipboard.topic, description: clipboard.description },
      topic: 'Google AI工具一次掌握',
      description: '不需技術基礎',
    });

    component.save();

    const req = httpMock.expectOne(itemsUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.promotionPkid).toBe(11);
    expect(req.request.body.slot).toBe(2);
    req.flush({ ...existing, pkid: 9, promotionPkid: 11 });
  });

  // ---------- Edit mode ----------

  it('loads the existing values in edit mode', async () => {
    await setup({ item: existing });

    expect(component.isEditMode()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('編輯');

    const value = component.form.getRawValue();
    expect(value.promotion.pkid).toBe(10);
    expect(value.promotion.promoCode).toBe('20251204_SkillTrainAI');
    expect(value.topic).toBe('成為能AI協作的程式設計師');
    expect(value.description).toBe('轉職就業養成班');
  });

  it('ignores a prefill in edit mode — the record wins', async () => {
    await setup({ item: existing, prefill: clipboard });

    expect(component.form.getRawValue().promotion.pkid).toBe(10);
  });

  it('PUTs to the collection route with the key in the body after re-looking-up the code', async () => {
    await setup({ item: existing });

    component.onPromotionSelected(n8n);
    component.form.patchValue({ topic: '改過的主題' });
    component.save();

    const req = httpMock.expectOne(itemsUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      pkid: 1,
      scheduleOn: '2026-03-16',
      trainingCenterPkid: 1,
      slot: 2,
      promotionPkid: 12,
      topic: '改過的主題',
      description: '從自動化新手到企業級AI架構師',
    });

    req.flush({ ...existing, promotionPkid: 12, topic: '改過的主題' });
    expect(savedItems.length).toBe(1);
  });

  it('cancel emits cancelled without touching the API', async () => {
    await setup({ item: existing });

    component.cancel();

    httpMock.expectNone(itemsUrl);
    expect(cancelledCount).toBe(1);
  });
});
