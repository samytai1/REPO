import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { PublishStatus } from '@core/models';
import { PublishStatusList } from './publish-status-list';

// Regression: ISSUE-001 — a header click never wrote `publish-status-list-sort` and reset the
//   stored page to 0, because `onStateChange` read `sortField`/`first` off an `(onSort)` event
//   that carries `{ field, order }`. Driven through the real header rather than a hand-built event.
// Found by /qa on 2026-09-10
// Report: .gstack/qa-reports/qa-report-localhost-4200-2026-09-10.md

/** Reaches the component's protected members from the spec. */
type ListInternals = {
  sort: { field: string; order: number };
  page: { first: number; rows: number };
};

describe('PublishStatusList (regression ISSUE-001: sort persistence from the real (onSort) event)', () => {
  const queryUrl = `${environment.apiBaseUrl}/publish-statuses/query`;

  const statuses: PublishStatus[] = [
    { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
    { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
  ];

  let fixture: ComponentFixture<PublishStatusList>;
  let component: ListInternals;
  let httpMock: HttpTestingController;

  function flushInitialLoad(): void {
    httpMock.expectOne(queryUrl).flush(statuses);
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
      imports: [PublishStatusList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance as unknown as ListInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('writes publish-status-list-sort when a column header is clicked, and keeps the rows-per-page', () => {
    flushInitialLoad();
    sessionStorage.setItem('publish-status-list-page', JSON.stringify({ first: 0, rows: 50 }));
    component.page = { first: 0, rows: 50 };

    clickHeader('description');

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort') ?? 'null')).toEqual({
      field: 'description',
      order: 1,
    });
    expect(component.sort).toEqual({ field: 'description', order: 1 });
    // The sort event carries no `rows`; the setting must not fall back to the default.
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page') ?? 'null')).toEqual({
      first: 0,
      rows: 50,
    });
  });
});
