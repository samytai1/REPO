import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';

import { Partner } from '@core/models';
import { PartnerService } from '@core/services';

/**
 * 合作夥伴 Partner — read-only detail page.
 *
 * Certification / Course / PartnerCourseGroup all FK into this table, but none of those features
 * exists yet, so the 對應… link buttons are deferred (see spec/course/Partner.md).
 */
@Component({
  selector: 'app-partner-detail',
  imports: [ButtonModule],
  templateUrl: './partner-detail.html',
  styleUrl: './partner-detail.scss',
})
export class PartnerDetail implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const rawPkid = this.route.snapshot.paramMap.get('id');
    const pkid = Number(rawPkid);

    if (rawPkid === null || !Number.isInteger(pkid)) {
      this.loading.set(false);
      return;
    }

    this.service.getById(pkid).subscribe({
      next: (partner) => {
        this.partner.set(partner);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '找不到此合作夥伴。',
        });
      },
    });
  }

  protected back(): void {
    void this.router.navigate(['/partners']);
  }

  protected edit(): void {
    const partner = this.partner();
    if (partner) {
      void this.router.navigate(['/partners', partner.pkid, 'edit']);
    }
  }
}
