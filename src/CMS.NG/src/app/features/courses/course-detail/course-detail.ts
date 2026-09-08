import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { TagModule } from 'primeng/tag';

import { CertificationLookup, Course, certificationLabel } from '@core/models';
import { CourseService } from '@core/services';
import { QrCode } from '@shared/qr-code/qr-code';

/** Public 課程介紹 page the 基本資料 QR code points at: `{base}/{pkid}/{courseId}`. */
const COURSE_SHOW_URL_BASE = 'https://www.uuu.com.tw/Course/Show';

/**
 * 課程 Course — read-only detail page.
 *
 * 原廠 and 上架狀態 link to their detail pages (both routes exist). 課程群組 stays plain text until
 * the CourseGroup feature is generated, and the CourseFAQ / CourseRelatedLink / HotCourse link
 * buttons are deferred for the same reason (see spec/course/Course.md).
 */
@Component({
  selector: 'app-course-detail',
  imports: [DecimalPipe, RouterLink, ButtonModule, TagModule, QrCode],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const rawPkid = this.route.snapshot.paramMap.get('id');
    const pkid = Number(rawPkid);

    if (rawPkid === null || !Number.isInteger(pkid)) {
      this.loading.set(false);
      return;
    }

    this.service.getById(pkid).subscribe({
      next: (course) => {
        this.course.set(course);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '找不到此課程。',
        });
      },
    });
  }

  /** The public course page encoded into the 基本資料 QR code. */
  protected courseShowUrl(course: Course): string {
    return `${COURSE_SHOW_URL_BASE}/${course.pkid}/${encodeURIComponent(course.courseId)}`;
  }

  /** A certification with a null title still needs a readable chip. */
  protected certificationLabel(certification: CertificationLookup): string {
    return certificationLabel(certification);
  }

  protected back(): void {
    void this.router.navigate(['/courses']);
  }

  protected edit(): void {
    const course = this.course();
    if (course) {
      void this.router.navigate(['/courses', course.pkid, 'edit']);
    }
  }
}
