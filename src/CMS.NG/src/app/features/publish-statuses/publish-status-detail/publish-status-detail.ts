import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { TagModule } from 'primeng/tag';

import { PublishStatus } from '@core/models';
import { PublishStatusService } from '@core/services';

/** 發布狀態 PublishStatus — read-only detail page. */
@Component({
  selector: 'app-publish-status-detail',
  imports: [ButtonModule, TagModule],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss',
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const rawPkid = this.route.snapshot.paramMap.get('id');
    const pkid = Number(rawPkid);

    if (rawPkid === null || !Number.isInteger(pkid)) {
      this.loading.set(false);
      return;
    }

    this.service.getById(pkid).subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '找不到此發布狀態。',
        });
      },
    });
  }

  protected back(): void {
    void this.router.navigate(['/publish-statuses']);
  }

  protected edit(): void {
    const status = this.status();
    if (status) {
      void this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
    }
  }
}
