import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';
import QRCode from 'qrcode';

import { Course } from '@core/models';
import { CourseDetail } from './course-detail';

type DetailInternals = {
  course: () => Course | null;
  loading: () => boolean;
  back(): void;
  edit(): void;
};

describe('CourseDetail', () => {
  const azure: Course = {
    pkid: 1,
    title: 'Azure 基礎架構',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'MS-AZ104',
    friendlyUrl: 'azure-admin',
    displayOrder: 20,
    partnerPkid: 1,
    courseGroupPkid: 10,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 30,
    listPrice: 24000,
    learningCredit: 30,
    material: '講義一冊',
    objective: null,
    target: null,
    prerequisites: null,
    outline: '第一章 概論\n第二章 實作',
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: '微軟' },
    courseGroup: { pkid: 10, description: '雲端' },
    publishStatus: { pkid: 2, description: '已發布' },
    jobCategoryCount: 2,
    certificationCount: 2,
    jobCategories: [
      { pkid: 5, description: '系統管理' },
      { pkid: 6, description: '網路管理' },
    ],
    certifications: [
      { pkid: 100, title: 'AZ-104', partnerPkid: 1 },
      { pkid: 300, title: null, partnerPkid: 1 },
    ],
  };

  let fixture: ComponentFixture<CourseDetail>;
  let component: DetailInternals;
  let httpMock: HttpTestingController;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseDetail],
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

    fixture = TestBed.createComponent(CourseDetail);
    component = fixture.componentInstance as unknown as DetailInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
  }

  function flush(course: Course = azure): void {
    httpMock.expectOne(`${environment.apiBaseUrl}/courses/${course.pkid}`).flush(course);
    fixture.detectChanges();
  }

  /** The QR code encodes asynchronously; wait for the data URL before reading the `<img>`. */
  async function flushQrCode(course: Course = azure): Promise<void> {
    flush(course);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('loads the course named in the route', async () => {
    await setup('1');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/courses/1`);
    expect(req.request.method).toBe('GET');
    req.flush(azure);
    fixture.detectChanges();

    expect(component.course()?.title).toBe('Azure 基礎架構');
    expect(component.loading()).toBeFalse();
  });

  it('renders every section of the course', async () => {
    await setup('1');
    flush();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('基本資料');
    expect(text).toContain('Azure 基礎架構');
    expect(text).toContain('Microsoft Azure Administrator');
    expect(text).toContain('AZ-104');
    expect(text).toContain('MS-AZ104');
    expect(text).toContain('azure-admin');
    expect(text).toContain('分類與狀態');
    expect(text).toContain('微軟');
    expect(text).toContain('雲端');
    expect(text).toContain('已發布');
    expect(text).toContain('排程與費用');
    expect(text).toContain('2026-01-01');
    expect(text).toContain('2036-01-01');
    expect(text).toContain('24,000');
    expect(text).toContain('30.0');
    expect(text).toContain('課程內容');
    expect(text).toContain('講義一冊');
    expect(text).toContain('第二章 實作');
  });

  it('links 原廠 and 上架狀態 to their detail pages', async () => {
    await setup('1');
    flush();

    const links = fixture.nativeElement.querySelectorAll('a.course-detail__link');
    expect(links.length).toBe(2);
    expect(links[0].getAttribute('href')).toBe('/partners/1');
    expect(links[0].textContent.trim()).toContain('微軟');
    expect(links[1].getAttribute('href')).toBe('/publish-statuses/2');
    expect(links[1].textContent.trim()).toContain('已發布');
  });

  it('renders 允許重聽 as a tag', async () => {
    await setup('1');
    flush();

    const tags = Array.from(
      fixture.nativeElement.querySelectorAll('p-tag') as NodeListOf<HTMLElement>,
    ).map((tag) => tag.textContent?.trim());

    expect(tags).toContain('是');
  });

  it('renders the n-n members as chips, with a fallback label for a null certification title', async () => {
    await setup('1');
    flush();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('職務類別（2）');
    expect(text).toContain('系統管理');
    expect(text).toContain('網路管理');
    expect(text).toContain('對應認證（2）');
    expect(text).toContain('AZ-104');
    expect(text).toContain('(未命名 #300)');
  });

  it('falls back to dashes and empty-state text for null values', async () => {
    await setup('2');
    flush({
      ...azure,
      pkid: 2,
      officialTitle: null,
      courseGroupPkid: null,
      courseGroup: null,
      material: null,
      outline: null,
      canRepeat: false,
      jobCategoryCount: 0,
      certificationCount: 0,
      jobCategories: [],
      certifications: [],
    });

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('尚無職務類別');
    expect(text).toContain('尚無對應認證');

    const dashes = Array.from(
      fixture.nativeElement.querySelectorAll('.detail-grid dd .empty-text') as NodeListOf<HTMLElement>,
    ).filter((el) => el.textContent?.trim() === '—');
    // officialTitle, courseGroup and the eight content columns are all null.
    expect(dashes.length).toBe(10);
  });

  it('does not call the API when the route carries no usable key', async () => {
    await setup(null);

    httpMock.expectNone(`${environment.apiBaseUrl}/courses/NaN`);
    expect(component.loading()).toBeFalse();
    expect(component.course()).toBeNull();
  });

  it('falls back to the empty state when the course is not found', async () => {
    await setup('99');

    httpMock
      .expectOne(`${environment.apiBaseUrl}/courses/99`)
      .flush('Not Found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(component.course()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('查無資料');
  });

  it('renders a QR code in 基本資料 encoding the public course URL', async () => {
    await setup('1');
    const encode = spyOn(QRCode, 'toDataURL').and.callThrough();
    await flushQrCode();

    const basicSection: HTMLElement = fixture.nativeElement.querySelector(
      '.course-detail__section',
    );
    const qr: HTMLElement = basicSection.querySelector('app-qr-code')!;
    expect(qr).withContext('the QR code belongs to the 基本資料 card').toBeTruthy();

    expect(encode.calls.mostRecent().args[0]).toBe('https://www.uuu.com.tw/Course/Show/1/AZ-104');

    const img: HTMLImageElement = qr.querySelector('img.qr-code__image')!;
    expect(img.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    expect(qr.querySelector('.qr-code__caption')?.textContent?.trim()).toBe('AZ-104');
    expect(img.getAttribute('alt')).toBe('AZ-104');
  });

  it('escapes a 簡介代碼 that is not URL-safe', async () => {
    await setup('7');
    const encode = spyOn(QRCode, 'toDataURL').and.callThrough();
    await flushQrCode({ ...azure, pkid: 7, courseId: 'AZ 104/A' });

    expect(encode.calls.mostRecent().args[0]).toBe(
      'https://www.uuu.com.tw/Course/Show/7/AZ%20104%2FA',
    );
  });

  it('downloads the QR code as a PNG named after 簡介代碼', async () => {
    await setup('1');
    await flushQrCode();

    const anchor = document.createElement('a');
    const click = spyOn(anchor, 'click');
    const create = document.createElement.bind(document);
    spyOn(document, 'createElement').and.callFake(
      (tag: string) => (tag === 'a' ? anchor : create(tag)) as HTMLElement,
    );

    const download: HTMLButtonElement = fixture.nativeElement.querySelector(
      'app-qr-code p-button button',
    );
    expect(download.textContent).toContain('下載');
    download.click();

    expect(click).toHaveBeenCalledTimes(1);
    expect(anchor.download).toBe('AZ-104.png');
    expect(anchor.getAttribute('href')).toMatch(/^data:image\/png;base64,/);
  });

  it('navigates back to the list and into the edit form', async () => {
    await setup('1');
    flush();

    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.back();
    expect(navigate).toHaveBeenCalledWith(['/courses']);

    component.edit();
    expect(navigate).toHaveBeenCalledWith(['/courses', 1, 'edit']);
  });
});
