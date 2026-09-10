import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { Partner } from '@core/models';
import { PartnerList } from './partner-list';

// Regression: ISSUE-001 — a header click never wrote `partner-list-sort` and reset the stored page
//   to 0, because `onStateChange` read `sortField`/`first` off an `(onSort)` event that carries
//   `{ field, order }`. Driven through the real header rather than a hand-built event.
// Found by /qa on 2026-09-10
// Report: .gstack/qa-reports/qa-report-localhost-4200-2026-09-10.md

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  sort: { field: string; order: number };
  page: { first: number; rows: number };
};

describe('PartnerList (regression ISSUE-001: sort persistence from the real (onSort) event)', () => {
  const queryUrl = `${environment.apiBaseUrl}/partners/query`;

  const partners: Partner[] = [
    {
      pkid: 2,
      name: '思科',
      appKey: 'CISCO',
      nameOnPartnerMenu: 'Cisco 思科課程',
      nameOnCourseDetailPage: '思科',
      displayOrder: 10,
      imageFilename: 'cisco.png',
    },
    {
      pkid: 3,
      name: '自辦課程',
      appKey: 'OWN',
      nameOnPartnerMenu: '恆逸自辦課程',
      nameOnCourseDetailPage: '恆逸',
      displayOrder: 30,
      imageFilename: null,
    },
  ];

  let fixture: ComponentFixture<PartnerList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;

  function flushInitialLoad(): void {
    httpMock.expectOne(queryUrl).flush(partners);
    fixture.detectChanges();
  }

  function clickHeader(field: string): void {
    const header = fixture.nativeElement.querySelector(
      `th[psortablecolumn="${field}"]`,
    ) as HTMLElement | null;
    expect(header).withContext(`a sortable ${field} header`).not.toBeNull();
    header!.click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('writes partner-list-sort when a column header is clicked, and keeps the rows-per-page', () => {
    flushInitialLoad();
    sessionStorage.setItem('partner-list-page', JSON.stringify({ first: 0, rows: 50 }));
    component.page = { first: 0, rows: 50 };

    clickHeader('name');

    expect(JSON.parse(sessionStorage.getItem('partner-list-sort') ?? 'null')).toEqual({
      field: 'name',
      order: 1,
    });
    expect(component.sort).toEqual({ field: 'name', order: 1 });
    // The sort event carries no `rows`; the setting must not fall back to the default.
    expect(JSON.parse(sessionStorage.getItem('partner-list-page') ?? 'null')).toEqual({
      first: 0,
      rows: 50,
    });
  });
});
