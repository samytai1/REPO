import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { TagModule } from 'primeng/tag';

import { AppRole } from '@core/models';
import { AppRoleService } from '@core/services';

/** 角色 AppRole — read-only detail page. */
@Component({
  selector: 'app-app-role-detail',
  imports: [ButtonModule, TagModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss',
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const roleId = this.route.snapshot.paramMap.get('id');

    if (!roleId) {
      this.loading.set(false);
      return;
    }

    this.service.getById(roleId).subscribe({
      next: (role) => {
        this.role.set(role);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '找不到此角色。',
        });
      },
    });
  }

  protected back(): void {
    void this.router.navigate(['/app-roles']);
  }

  protected edit(): void {
    const role = this.role();
    if (role) {
      void this.router.navigate(['/app-roles', role.roleId, 'edit']);
    }
  }
}
