import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { forkJoin, of } from 'rxjs';

import {
  CertificationLookup,
  Course,
  CourseGroupLookup,
  CourseRequest,
  CourseUpdateRequest,
  JobCategoryLookup,
  Partner,
  PublishStatus,
  certificationLabel,
} from '@core/models';
import { CourseService, LookupService } from '@core/services';
import { fromIso, toIso } from '@core/utils/date.util';

/** How far past 上架日期 the default 下架日期 lands when the user has not set one. */
const DEFAULT_SCHEDULE_YEARS = 10;

interface SelectOption<T> {
  label: string;
  value: T;
}

/** 課程 Course — shared add/edit form. Mode is derived from the route. */
@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TextareaModule,
    ToggleSwitchModule,
  ],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss',
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  /** Null in add mode; the key of the record being edited otherwise. */
  protected readonly editingPkid = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatus[]>([]);
  protected readonly jobCategories = signal<JobCategoryLookup[]>([]);
  protected readonly certifications = signal<CertificationLookup[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    pkid: [0],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    officialTitle: ['', [Validators.maxLength(300)]],
    courseId: ['', [Validators.required, Validators.maxLength(50)]],
    prodCourseId: ['', [Validators.required, Validators.maxLength(50)]],
    friendlyUrl: ['', [Validators.required, Validators.maxLength(100)]],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    partnerPkid: [null as number | null, [Validators.required]],
    courseGroupPkid: [null as number | null],
    publishStatusPkid: [null as number | null, [Validators.required]],
    scheduleOn: [null as Date | null, [Validators.required]],
    scheduleOff: [null as Date | null, [Validators.required]],
    hour: [0, [Validators.required, Validators.min(0)]],
    listPrice: [0, [Validators.required, Validators.min(0)]],
    learningCredit: [0, [Validators.required, Validators.min(0)]],
    material: ['', [Validators.maxLength(500)]],
    objective: ['', [Validators.maxLength(4000)]],
    target: ['', [Validators.maxLength(500)]],
    prerequisites: ['', [Validators.maxLength(4000)]],
    outline: [''],
    towardCertOrExam: [''],
    note: ['', [Validators.maxLength(4000)]],
    otherInfo: ['', [Validators.maxLength(4000)]],
    canRepeat: [false],
    jobCategoryPkids: [[] as number[]],
    certificationPkids: [[] as number[]],
  });

  constructor() {
    // Add mode only: default 下架日期 to 上架日期 + 10 years until the user sets it themselves.
    // emitEvent:false keeps the write from re-entering this subscription.
    this.form.controls.scheduleOn.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((scheduleOn) => {
        const scheduleOff = this.form.controls.scheduleOff;
        if (this.isEditMode || !scheduleOn || scheduleOff.dirty) return;

        const defaulted = new Date(scheduleOn);
        defaulted.setFullYear(defaulted.getFullYear() + DEFAULT_SCHEDULE_YEARS);
        scheduleOff.setValue(defaulted, { emitEvent: false });
      });
  }

  protected get isEditMode(): boolean {
    return this.editingPkid() !== null;
  }

  protected get partnerOptions(): SelectOption<number>[] {
    return this.partners().map((p) => ({ label: p.name, value: p.pkid }));
  }

  /** The nullable FK gets an explicit 無 option so the user can clear it. */
  protected get courseGroupOptions(): SelectOption<number | null>[] {
    return [
      { label: '無', value: null },
      ...this.courseGroups().map((g) => ({ label: g.description, value: g.pkid })),
    ];
  }

  protected get publishStatusOptions(): SelectOption<number>[] {
    return this.publishStatuses().map((s) => ({ label: s.description, value: s.pkid }));
  }

  protected get jobCategoryOptions(): SelectOption<number>[] {
    return this.jobCategories().map((jc) => ({ label: jc.description, value: jc.pkid }));
  }

  protected get certificationOptions(): SelectOption<number>[] {
    return this.certifications().map((ct) => ({ label: certificationLabel(ct), value: ct.pkid }));
  }

  ngOnInit(): void {
    const rawPkid = this.route.snapshot.paramMap.get('id');
    const pkid = rawPkid === null ? null : Number(rawPkid);
    const editing = pkid !== null && Number.isInteger(pkid);

    if (editing) this.editingPkid.set(pkid);

    // The five lookups and the edited record load in parallel.
    forkJoin({
      partners: this.lookupService.getPartners(),
      courseGroups: this.lookupService.getCourseGroups(),
      publishStatuses: this.lookupService.getPublishStatuses(),
      jobCategories: this.lookupService.getJobCategories(),
      certifications: this.lookupService.getCertifications(),
      course: editing ? this.service.getById(pkid) : of(null),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, jobCategories, certifications, course }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
        this.jobCategories.set(jobCategories);
        this.certifications.set(certifications);

        if (course) {
          this.patchFrom(course);
          // pkid is an IDENTITY key — shown, never edited.
          this.form.controls.pkid.disable();
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得表單資料。',
        });
      },
    });
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled pkid control in edit mode.
    const value = this.form.getRawValue();
    const scheduleOn = toIso(value.scheduleOn);
    const scheduleOff = toIso(value.scheduleOff);

    // The validators already require these; the guard narrows the types for the request.
    if (value.partnerPkid === null || value.publishStatusPkid === null || !scheduleOn || !scheduleOff) {
      this.form.markAllAsTouched();
      return;
    }

    const request: CourseRequest = {
      title: value.title.trim(),
      officialTitle: nullIfBlank(value.officialTitle),
      courseId: value.courseId.trim(),
      prodCourseId: value.prodCourseId.trim(),
      friendlyUrl: value.friendlyUrl.trim(),
      displayOrder: value.displayOrder,
      partnerPkid: value.partnerPkid,
      courseGroupPkid: value.courseGroupPkid,
      publishStatusPkid: value.publishStatusPkid,
      scheduleOn,
      scheduleOff,
      hour: value.hour,
      listPrice: value.listPrice,
      learningCredit: value.learningCredit,
      // Nullable columns: send null, never an empty string.
      material: nullIfBlank(value.material),
      objective: nullIfBlank(value.objective),
      target: nullIfBlank(value.target),
      prerequisites: nullIfBlank(value.prerequisites),
      outline: nullIfBlank(value.outline),
      towardCertOrExam: nullIfBlank(value.towardCertOrExam),
      note: nullIfBlank(value.note),
      otherInfo: nullIfBlank(value.otherInfo),
      canRepeat: value.canRepeat,
      jobCategoryPkids: value.jobCategoryPkids,
      certificationPkids: value.certificationPkids,
    };

    this.saving.set(true);

    const request$ = this.isEditMode
      ? this.service.update({ ...request, pkid: value.pkid } satisfies CourseUpdateRequest)
      : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEditMode ? '已更新' : '已新增',
          detail: `課程「${saved.title}」已儲存。`,
        });
        void this.router.navigate(['/courses', saved.pkid]);
      },
      error: () => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: '請確認輸入內容後再試一次。',
        });
      },
    });
  }

  protected cancel(): void {
    const pkid = this.editingPkid();
    void this.router.navigate(pkid === null ? ['/courses'] : ['/courses', pkid]);
  }

  private patchFrom(course: Course): void {
    this.form.patchValue({
      pkid: course.pkid,
      title: course.title,
      officialTitle: course.officialTitle ?? '',
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: fromIso(course.scheduleOn),
      scheduleOff: fromIso(course.scheduleOff),
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material ?? '',
      objective: course.objective ?? '',
      target: course.target ?? '',
      prerequisites: course.prerequisites ?? '',
      outline: course.outline ?? '',
      towardCertOrExam: course.towardCertOrExam ?? '',
      note: course.note ?? '',
      otherInfo: course.otherInfo ?? '',
      canRepeat: course.canRepeat,
      jobCategoryPkids: course.jobCategories.map((jc) => jc.pkid),
      certificationPkids: course.certifications.map((ct) => ct.pkid),
    });
  }
}

/** Trims a nullable text control; blank becomes null so the column never stores ''. */
function nullIfBlank(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
