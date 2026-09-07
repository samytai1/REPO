import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { Partner } from '@core/models';
import { PartnerDetail } from './partner-detail';

type DetailInternals = {
  partner: () => Partner | null;
  loading: () => boolean;
  back(): void;
  edit(): void;
};

describe('PartnerDetail', () => {
  const cisco: Partner = {
    pkid: 2,
    name: '思科',
    appKey: 'CISCO',
    nameOnPartnerMenu: 'Cisco 思科課程',
    nameOnCourseDetailPage: '思科',
    displayOrder: 10,
    imageFilename: 'cisco.png',
  };

  let fixture: ComponentFixture<PartnerDetail>;
  let component: DetailInternals;
  let httpMock: HttpTestingController;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => pkid } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    component = fixture.componentInstance as unknown as DetailInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('loads the partner named in the route', async () => {
    await setup('2');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/partners/2`);
    expect(req.request.method).toBe('GET');
    req.flush(cisco);
    fixture.detectChanges();

    expect(component.partner()?.name).toBe('思科');
    expect(component.loading()).toBeFalse();
  });

  it('renders every field of the partner', async () => {
    await setup('2');
    httpMock.expectOne(`${environment.apiBaseUrl}/partners/2`).flush(cisco);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('主代碼');
    expect(text).toContain('2');
    expect(text).toContain('思科');
    expect(text).toContain('CISCO');
    expect(text).toContain('Cisco 思科課程');
    expect(text).toContain('顯示順序');
    expect(text).toContain('cisco.png');
  });

  it('falls back to a dash when the partner has no image filename', async () => {
    await setup('3');
    httpMock
      .expectOne(`${environment.apiBaseUrl}/partners/3`)
      .flush({ ...cisco, pkid: 3, name: '自辦課程', imageFilename: null });
    fixture.detectChanges();

    const values = fixture.nativeElement.querySelectorAll('.detail-grid dd');
    expect(values[values.length - 1].textContent.trim()).toBe('—');
  });

  it('does not call the API when the route carries no usable key', async () => {
    await setup(null);

    httpMock.expectNone(`${environment.apiBaseUrl}/partners/NaN`);
    expect(component.loading()).toBeFalse();
    expect(component.partner()).toBeNull();
  });

  it('falls back to the empty state when the partner is not found', async () => {
    await setup('99');

    httpMock
      .expectOne(`${environment.apiBaseUrl}/partners/99`)
      .flush('Not Found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(component.partner()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('navigates back to the list and into the edit form', async () => {
    await setup('2');
    httpMock.expectOne(`${environment.apiBaseUrl}/partners/2`).flush(cisco);
    fixture.detectChanges();

    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.back();
    expect(navigate).toHaveBeenCalledWith(['/partners']);

    component.edit();
    expect(navigate).toHaveBeenCalledWith(['/partners', 2, 'edit']);
  });
});
