import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AdminService, AuditLog, LoginHistory } from '../../../core/admin/admin.service';

@Component({
  standalone: true,
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatTabsModule, MatTableModule],
  templateUrl: './admin-activity.component.html',
  styleUrl: './admin-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminActivityComponent {
  readonly logins = signal<LoginHistory[]>([]);
  readonly audits = signal<AuditLog[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly loginColumns = ['email', 'result', 'ip', 'time'];
  readonly auditColumns = ['action', 'entity', 'time'];
  private readonly admin = inject(AdminService);

  constructor() { this.load(); }
  load(): void {
    this.loading.set(true); this.error.set(false);
    this.admin.getLoginHistory().subscribe({ next: (items) => this.logins.set(items), error: () => this.error.set(true) });
    this.admin.getAuditLogs().subscribe({ next: (items) => this.audits.set(items), error: () => this.error.set(true), complete: () => this.loading.set(false) });
  }
}

