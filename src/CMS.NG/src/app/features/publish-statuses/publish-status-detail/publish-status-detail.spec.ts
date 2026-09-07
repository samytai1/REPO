import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { PublishStatus } from '@core/models';
import { PublishStatusDetail } from './publish-status-detail';

type DetailInternals = {
  status: () => PublishStatus | null;
  loading: () => boolean;
  back(): void;
  edit(): void;
};

describe('PublishStatusDetail', () => {
  const discontinued: PublishStatus = {
    pkid: 3,
    description: '已下架',
    isDraft: false,
    isPublished: false,
    isDiscontinued: true,
  };

  let fixture: ComponentFixture<PublishStatusDetail>;
  let component: DetailInternals;
  let httpMock: HttpTestingController;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => pkid } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusDetail);
    component = fixture.componentInstance as unknown as DetailInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('loads the status named in the route', async () => {
    await setup('3');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/publish-statuses/3`);
    expect(req.request.method).toBe('GET');
    req.flush(discontinued);
    fixture.detectChanges();

    expect(component.status()?.description).toBe('已下架');
    expect(component.loading()).toBeFalse();
  });

  it('renders every field of the status', async () => {
    await setup('3');
    httpMock.expectOne(`${environment.apiBaseUrl}/publish-statuses/3`).flush(discontinued);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('主代碼');
    expect(text).toContain('3');
    expect(text).toContain('已下架');
    expect(text).toContain('草稿');
    expect(text).toContain('已發布');
  });

  it('renders the bit fields as 是／否 tags', async () => {
    await setup('3');
    httpMock.expectOne(`${environment.apiBaseUrl}/publish-statuses/3`).flush(discontinued);
    fixture.detectChanges();

    const tags = fixture.nativeElement.querySelectorAll('.detail-grid p-tag');
    expect(tags.length).toBe(3);
    expect(tags[0].textContent.trim()).toBe('否');
    expect(tags[2].textContent.trim()).toBe('是');
  });

  it('does not call the API when the route carries no usable key', async () => {
    await setup(null);

    httpMock.expectNone(`${environment.apiBaseUrl}/publish-statuses/NaN`);
    expect(component.loading()).toBeFalse();
    expect(component.status()).toBeNull();
  });

  it('falls back to the empty state when the status is not found', async () => {
    await setup('99');

    httpMock
      .expectOne(`${environment.apiBaseUrl}/publish-statuses/99`)
      .flush('Not Found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(component.status()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('navigates back to the list and into the edit form', async () => {
    await setup('3');
    httpMock.expectOne(`${environment.apiBaseUrl}/publish-statuses/3`).flush(discontinued);
    fixture.detectChanges();

    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.back();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses']);

    component.edit();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 3, 'edit']);
  });
});
