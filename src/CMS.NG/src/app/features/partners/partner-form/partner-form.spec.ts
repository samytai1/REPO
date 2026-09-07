import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { Partner } from '@core/models';
import { PartnerForm } from './partner-form';

type FormInternals = {
  form: FormGroup;
  loading: () => boolean;
  saving: () => boolean;
  isEditMode: boolean;
  save(): void;
  cancel(): void;
};

describe('PartnerForm', () => {
  const baseUrl = `${environment.apiBaseUrl}/partners`;

  const microsoft: Partner = {
    pkid: 1,
    name: '微軟',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟課程',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 20,
    imageFilename: 'ms-logo.png',
  };

  let fixture: ComponentFixture<PartnerForm>;
  let component: FormInternals;
  let httpMock: HttpTestingController;

  /** `pkid` null => add mode; a value => edit mode. */
  async function setup(pkid: string | null, record: Partner = microsoft): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PartnerForm],
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

    fixture = TestBed.createComponent(PartnerForm);
    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();

    if (pkid) {
      httpMock.expectOne(`${baseUrl}/${pkid}`).flush(record);
    }
    fixture.detectChanges();
  }

  /** Fills every required control with a valid value. */
  function fillRequired(): void {
    component.form.patchValue({
      name: '紅帽',
      appKey: 'RH',
      nameOnPartnerMenu: 'Red Hat 紅帽課程',
      nameOnCourseDetailPage: '紅帽',
      displayOrder: 40,
    });
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  it('starts empty in add mode and issues no read', async () => {
    await setup(null);

    expect(component.isEditMode).toBeFalse();
    expect(component.loading()).toBeFalse();
    expect(component.form.getRawValue()).toEqual({
      pkid: 0,
      name: '',
      appKey: '',
      nameOnPartnerMenu: '',
      nameOnCourseDetailPage: '',
      displayOrder: 0,
      imageFilename: '',
    });
  });

  it('shows the add-mode heading and hides the IDENTITY key field', async () => {
    await setup(null);

    expect(fixture.nativeElement.textContent).toContain('新增合作夥伴');
    expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
  });

  it('does not submit while a required field is missing', async () => {
    await setup(null);

    component.save();

    httpMock.expectNone(baseUrl);
    expect(component.form.controls['name'].touched).toBeTrue();
    expect(component.form.controls['appKey'].touched).toBeTrue();
  });

  it('rejects a negative display order', async () => {
    await setup(null);

    fillRequired();
    component.form.patchValue({ displayOrder: -1 });
    component.save();

    httpMock.expectNone(baseUrl);
    expect(component.form.controls['displayOrder'].invalid).toBeTrue();
  });

  it('POSTs a trimmed, keyless request on save in add mode', async () => {
    await setup(null);

    component.form.patchValue({
      name: '  紅帽  ',
      appKey: '  RH  ',
      nameOnPartnerMenu: '  Red Hat 紅帽課程  ',
      nameOnCourseDetailPage: '  紅帽  ',
      displayOrder: 40,
      imageFilename: '  rh.png  ',
    });

    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: '紅帽',
      appKey: 'RH',
      nameOnPartnerMenu: 'Red Hat 紅帽課程',
      nameOnCourseDetailPage: '紅帽',
      displayOrder: 40,
      imageFilename: 'rh.png',
    });
    expect(req.request.body.pkid).toBeUndefined();

    req.flush({ ...microsoft, pkid: 4, name: '紅帽' });
    expect(component.saving()).toBeFalse();
  });

  it('sends null, not an empty string, for a blank image filename', async () => {
    await setup(null);

    fillRequired();
    component.form.patchValue({ imageFilename: '   ' });
    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.body.imageFilename).toBeNull();
    req.flush({ ...microsoft, pkid: 4 });
  });

  it('navigates to the saved record after a successful create', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fillRequired();
    component.save();
    httpMock.expectOne(baseUrl).flush({ ...microsoft, pkid: 4, name: '紅帽' });

    expect(navigate).toHaveBeenCalledWith(['/partners', 4]);
  });

  it('stops the saving state when the API rejects the request', async () => {
    await setup(null);

    fillRequired();
    component.save();

    httpMock.expectOne(baseUrl).flush('bad request', { status: 400, statusText: 'Bad Request' });

    expect(component.saving()).toBeFalse();
  });

  // ---------- Edit mode ----------

  it('loads the record in edit mode', async () => {
    await setup('1');

    expect(component.isEditMode).toBeTrue();
    expect(component.form.getRawValue()).toEqual({
      pkid: 1,
      name: '微軟',
      appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 微軟課程',
      nameOnCourseDetailPage: '微軟',
      displayOrder: 20,
      imageFilename: 'ms-logo.png',
    });
  });

  it('patches a null image filename to an empty control value', async () => {
    await setup('3', { ...microsoft, pkid: 3, name: '自辦課程', imageFilename: null });

    expect(component.form.getRawValue().imageFilename).toBe('');
  });

  it('locks the key field in edit mode', async () => {
    await setup('1');

    expect(component.form.controls['pkid'].disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('編輯合作夥伴');
    expect(fixture.nativeElement.querySelector('#pkid')).not.toBeNull();
  });

  it('PUTs to the collection route with the disabled key still in the body', async () => {
    await setup('1');

    component.form.patchValue({ name: '微軟（新）', displayOrder: 5 });
    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      pkid: 1,
      name: '微軟（新）',
      appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 微軟課程',
      nameOnCourseDetailPage: '微軟',
      displayOrder: 5,
      imageFilename: 'ms-logo.png',
    });

    req.flush({ ...microsoft, name: '微軟（新）', displayOrder: 5 });
  });

  it('can clear the image filename on edit', async () => {
    await setup('1');

    component.form.patchValue({ imageFilename: '' });
    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.body.imageFilename).toBeNull();
    req.flush({ ...microsoft, imageFilename: null });
  });

  it('cancel returns to the record in edit mode', async () => {
    await setup('1');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/partners', 1]);
  });

  it('cancel returns to the list in add mode', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/partners']);
  });
});
