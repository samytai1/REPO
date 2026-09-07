import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@environments/environment';

import { PublishStatus } from '@core/models';
import { PublishStatusForm } from './publish-status-form';

type FormInternals = {
  form: FormGroup;
  loading: () => boolean;
  saving: () => boolean;
  isEditMode: boolean;
  save(): void;
  cancel(): void;
};

describe('PublishStatusForm', () => {
  const baseUrl = `${environment.apiBaseUrl}/publish-statuses`;

  const draft: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
  };

  let fixture: ComponentFixture<PublishStatusForm>;
  let component: FormInternals;
  let httpMock: HttpTestingController;

  /** `pkid` null => add mode; a value => edit mode. */
  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusForm],
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

    fixture = TestBed.createComponent(PublishStatusForm);
    component = fixture.componentInstance as unknown as FormInternals;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges();

    if (pkid) {
      httpMock.expectOne(`${baseUrl}/${pkid}`).flush(draft);
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  it('starts empty in add mode and issues no read', async () => {
    await setup(null);

    expect(component.isEditMode).toBeFalse();
    expect(component.loading()).toBeFalse();
    expect(component.form.getRawValue()).toEqual({
      pkid: 0,
      description: '',
      isDraft: false,
      isPublished: false,
      isDiscontinued: false,
    });
  });

  it('shows the add-mode heading and leaves the key editable', async () => {
    await setup(null);

    expect(fixture.nativeElement.textContent).toContain('新增發布狀態');
    expect(component.form.controls['pkid'].disabled).toBeFalse();
  });

  it('does not submit while the description is missing', async () => {
    await setup(null);

    component.save();

    httpMock.expectNone(baseUrl);
    expect(component.form.controls['description'].touched).toBeTrue();
  });

  it('rejects a pkid outside the tinyint range', async () => {
    await setup(null);

    component.form.patchValue({ pkid: 300, description: '超出範圍' });
    component.save();

    httpMock.expectNone(baseUrl);
    expect(component.form.controls['pkid'].invalid).toBeTrue();
  });

  it('POSTs a trimmed request on save in add mode', async () => {
    await setup(null);

    component.form.patchValue({
      pkid: 4,
      description: '  審核中  ',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    });

    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      pkid: 4,
      description: '審核中',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    });

    req.flush({ ...draft, pkid: 4, description: '審核中' });
    expect(component.saving()).toBeFalse();
  });

  it('navigates to the saved record after a successful create', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.form.patchValue({ pkid: 4, description: '審核中' });
    component.save();
    httpMock.expectOne(baseUrl).flush({ ...draft, pkid: 4, description: '審核中' });

    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 4]);
  });

  it('stops the saving state when the API rejects a duplicate key', async () => {
    await setup(null);

    component.form.patchValue({ pkid: 1, description: '重複' });
    component.save();

    httpMock.expectOne(baseUrl).flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(component.saving()).toBeFalse();
  });

  // ---------- Edit mode ----------

  it('loads the record in edit mode', async () => {
    await setup('1');

    expect(component.isEditMode).toBeTrue();
    expect(component.form.getRawValue()).toEqual({
      pkid: 1,
      description: '草稿',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    });
  });

  it('locks the key field in edit mode', async () => {
    await setup('1');

    expect(component.form.controls['pkid'].disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('編輯發布狀態');
  });

  it('PUTs to the collection route with the disabled key still in the body', async () => {
    await setup('1');

    component.form.patchValue({ description: '草稿（新）', isDraft: false, isPublished: true });
    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      pkid: 1,
      description: '草稿（新）',
      isDraft: false,
      isPublished: true,
      isDiscontinued: false,
    });

    req.flush({ ...draft, description: '草稿（新）' });
  });

  it('can clear every flag', async () => {
    await setup('1');

    component.form.patchValue({ isDraft: false, isPublished: false, isDiscontinued: false });
    component.save();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.body.isDraft).toBeFalse();
    req.flush(draft);
  });

  it('cancel returns to the record in edit mode', async () => {
    await setup('1');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1]);
  });

  it('cancel returns to the list in add mode', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    component.cancel();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses']);
  });
});
