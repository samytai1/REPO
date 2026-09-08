import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { CertificationLookup, Course, CourseGroupLookup, JobCategoryLookup } from '@core/models';
import { CourseForm } from './course-form';

type FormInternals = {
  form: FormGroup;
  loading: () => boolean;
  saving: () => boolean;
  isEditMode: boolean;
  partnerOptions: { label: string; value: number }[];
  courseGroupOptions: { label: string; value: number | null }[];
  publishStatusOptions: { label: string; value: number }[];
  jobCategoryOptions: { label: string; value: number }[];
  certificationOptions: { label: string; value: number }[];
  save(): void;
  cancel(): void;
};

describe('CourseForm', () => {
  const coursesUrl = `${environment.apiBaseUrl}/courses`;
  const lookupsUrl = `${environment.apiBaseUrl}/lookups`;

  const courseGroups: CourseGroupLookup[] = [
    { pkid: 20, description: '資安' },
    { pkid: 10, description: '雲端' },
  ];

  const jobCategories: JobCategoryLookup[] = [
    { pkid: 5, description: '系統管理' },
    { pkid: 6, description: '網路管理' },
  ];

  const certifications: CertificationLookup[] = [
    { pkid: 100, title: 'AZ-104', partnerPkid: 1 },
    { pkid: 300, title: null, partnerPkid: 2 },
  ];

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
    material: null,
    objective: '學會 Azure',
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: '微軟' },
    courseGroup: { pkid: 10, description: '雲端' },
    publishStatus: { pkid: 2, description: '已發布' },
    jobCategoryCount: 1,
    certificationCount: 1,
    jobCategories: [jobCategories[0]],
    certifications: [certifications[0]],
  };

  let fixture: ComponentFixture<CourseForm>;
  let component: FormInternals;
  let httpMock: HttpTestingController;

  /** `pkid` null => add mode; a value => edit mode. */
  async function setup(pkid: string | null, course: Course = azure): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseForm],
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

    fixture = TestBed.createComponent(CourseForm);
    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();

    httpMock.expectOne(`${lookupsUrl}/partners`).flush([
      {
        pkid: 2,
        name: '思科',
        appKey: 'CISCO',
        nameOnPartnerMenu: 'Cisco',
        nameOnCourseDetailPage: '思科',
        displayOrder: 10,
        imageFilename: null,
      },
      {
        pkid: 1,
        name: '微軟',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 20,
        imageFilename: null,
      },
    ]);
    httpMock.expectOne(`${lookupsUrl}/course-groups`).flush(courseGroups);
    httpMock.expectOne(`${lookupsUrl}/publish-statuses`).flush([
      { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false },
      { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false },
    ]);
    httpMock.expectOne(`${lookupsUrl}/job-categories`).flush(jobCategories);
    httpMock.expectOne(`${lookupsUrl}/certifications`).flush(certifications);
    if (pkid) {
      httpMock.expectOne(`${coursesUrl}/${pkid}`).flush(course);
    }
    fixture.detectChanges();
  }

  /** Fills every required control so `save()` gets past validation. */
  function fillRequired(): void {
    component.form.patchValue({
      title: 'Linux 系統管理',
      courseId: 'LX-101',
      prodCourseId: 'UU-LX101',
      friendlyUrl: 'linux-admin',
      displayOrder: 40,
      partnerPkid: 1,
      publishStatusPkid: 2,
      scheduleOn: new Date(2026, 8, 1),
      scheduleOff: new Date(2036, 8, 1),
      hour: 24,
      listPrice: 18000,
      learningCredit: 24,
    });
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  it('starts empty in add mode', async () => {
    await setup(null);

    expect(component.isEditMode).toBeFalse();
    expect(component.loading()).toBeFalse();
    expect(component.form.getRawValue()).toEqual(
      jasmine.objectContaining({
        pkid: 0,
        title: '',
        partnerPkid: null,
        courseGroupPkid: null,
        publishStatusPkid: null,
        scheduleOn: null,
        scheduleOff: null,
        hour: 0,
        listPrice: 0,
        learningCredit: 0,
        canRepeat: false,
        jobCategoryPkids: [],
        certificationPkids: [],
      }),
    );
  });

  it('loads the five lookups in parallel and maps them to options', async () => {
    await setup(null);

    expect(component.partnerOptions.map((o) => o.label)).toEqual(['思科', '微軟']);
    expect(component.courseGroupOptions[0]).toEqual({ label: '無', value: null });
    expect(component.courseGroupOptions.map((o) => o.label)).toEqual(['無', '資安', '雲端']);
    expect(component.publishStatusOptions.map((o) => o.label)).toEqual(['草稿', '已發布']);
    expect(component.jobCategoryOptions.map((o) => o.value)).toEqual([5, 6]);
    expect(component.certificationOptions).toEqual([
      { label: 'AZ-104', value: 100 },
      { label: '(未命名 #300)', value: 300 },
    ]);
  });

  it('shows the add-mode heading and hides the key control', async () => {
    await setup(null);

    expect(fixture.nativeElement.textContent).toContain('新增課程');
    expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
  });

  it('does not submit while required fields are missing', async () => {
    await setup(null);

    component.save();

    httpMock.expectNone(coursesUrl);
    expect(component.form.controls['title'].touched).toBeTrue();
    expect(component.form.controls['partnerPkid'].touched).toBeTrue();
  });

  it('defaults 下架日期 to 上架日期 + 10 years while 下架日期 is untouched', async () => {
    await setup(null);

    component.form.controls['scheduleOn'].setValue(new Date(2026, 8, 15));

    const scheduleOff = component.form.controls['scheduleOff'].value as Date;
    expect(scheduleOff.getFullYear()).toBe(2036);
    expect(scheduleOff.getMonth()).toBe(8);
    expect(scheduleOff.getDate()).toBe(15);
  });

  it('stops defaulting 下架日期 once the user has set it', async () => {
    await setup(null);

    const scheduleOff = component.form.controls['scheduleOff'];
    scheduleOff.setValue(new Date(2030, 0, 1));
    scheduleOff.markAsDirty();

    component.form.controls['scheduleOn'].setValue(new Date(2026, 8, 15));

    expect((scheduleOff.value as Date).getFullYear()).toBe(2030);
  });

  it('POSTs a trimmed request with ISO dates, nulls and the n-n keys on save in add mode', async () => {
    await setup(null);

    fillRequired();
    component.form.patchValue({
      title: '  Linux 系統管理  ',
      officialTitle: '   ',
      courseGroupPkid: 10,
      material: '  講義  ',
      outline: '',
      canRepeat: true,
      jobCategoryPkids: [5, 6],
      certificationPkids: [100],
    });

    component.save();

    const req = httpMock.expectOne(coursesUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      title: 'Linux 系統管理',
      officialTitle: null,
      courseId: 'LX-101',
      prodCourseId: 'UU-LX101',
      friendlyUrl: 'linux-admin',
      displayOrder: 40,
      partnerPkid: 1,
      courseGroupPkid: 10,
      publishStatusPkid: 2,
      scheduleOn: '2026-09-01',
      scheduleOff: '2036-09-01',
      hour: 24,
      listPrice: 18000,
      learningCredit: 24,
      material: '講義',
      objective: null,
      target: null,
      prerequisites: null,
      outline: null,
      towardCertOrExam: null,
      note: null,
      otherInfo: null,
      canRepeat: true,
      jobCategoryPkids: [5, 6],
      certificationPkids: [100],
    });
    expect(req.request.body.pkid).toBeUndefined();

    req.flush({ ...azure, pkid: 4, title: 'Linux 系統管理' });
    expect(component.saving()).toBeFalse();
  });

  it('navigates to the saved record after a successful create', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fillRequired();
    component.save();
    httpMock.expectOne(coursesUrl).flush({ ...azure, pkid: 4 });

    expect(navigate).toHaveBeenCalledWith(['/courses', 4]);
  });

  it('stops the saving state when the API rejects the request', async () => {
    await setup(null);

    fillRequired();
    component.save();

    httpMock.expectOne(coursesUrl).flush('bad', { status: 400, statusText: 'Bad Request' });

    expect(component.saving()).toBeFalse();
  });

  // ---------- Edit mode ----------

  it('loads the record, parses its dates and maps its n-n members in edit mode', async () => {
    await setup('1');

    expect(component.isEditMode).toBeTrue();

    const value = component.form.getRawValue();
    expect(value.pkid).toBe(1);
    expect(value.title).toBe('Azure 基礎架構');
    expect(value.officialTitle).toBe('Microsoft Azure Administrator');
    expect(value.partnerPkid).toBe(1);
    expect(value.courseGroupPkid).toBe(10);
    expect(value.publishStatusPkid).toBe(2);
    expect((value.scheduleOn as Date).getFullYear()).toBe(2026);
    expect((value.scheduleOff as Date).getFullYear()).toBe(2036);
    expect(value.objective).toBe('學會 Azure');
    expect(value.material).toBe('');
    expect(value.canRepeat).toBeTrue();
    expect(value.jobCategoryPkids).toEqual([5]);
    expect(value.certificationPkids).toEqual([100]);
  });

  it('locks the key field in edit mode and renders it', async () => {
    await setup('1');

    expect(component.form.controls['pkid'].disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('編輯課程');
    expect(fixture.nativeElement.querySelector('#pkid')).not.toBeNull();
  });

  it('never auto-defaults 下架日期 in edit mode', async () => {
    await setup('1');

    component.form.controls['scheduleOn'].setValue(new Date(2027, 0, 1));

    expect((component.form.controls['scheduleOff'].value as Date).getFullYear()).toBe(2036);
  });

  it('PUTs to the collection route with the key still in the body', async () => {
    await setup('1');

    component.form.patchValue({
      title: 'Azure 基礎架構（改版）',
      courseGroupPkid: null,
      jobCategoryPkids: [6],
      certificationPkids: [],
    });
    component.save();

    const req = httpMock.expectOne(coursesUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(
      jasmine.objectContaining({
        pkid: 1,
        title: 'Azure 基礎架構（改版）',
        courseGroupPkid: null,
        scheduleOn: '2026-01-01',
        scheduleOff: '2036-01-01',
        jobCategoryPkids: [6],
        certificationPkids: [],
      }),
    );

    req.flush({ ...azure, title: 'Azure 基礎架構（改版）' });
  });

  it('cancel returns to the record in edit mode', async () => {
    await setup('1');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/courses', 1]);
  });

  it('cancel returns to the list in add mode', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/courses']);
  });
});
