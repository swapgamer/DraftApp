import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AdminService, AuditLog, LoginHistory } from '../../../core/admin/admin.service';
import { AuthService } from '../../../core/auth/auth.service';

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
  readonly clearing = signal<'login' | 'audit' | null>(null);
  readonly canClearHistory = computed(() => this.auth.user()?.isSystemAdmin ?? false);
  readonly loginColumns = ['email', 'result', 'ip', 'time'];
  readonly auditColumns = ['action', 'entity', 'time'];
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);

  constructor() { this.load(); }
  load(): void {
    this.loading.set(true); this.error.set(false);
    this.admin.getLoginHistory().subscribe({ next: (items) => this.logins.set(items), error: () => { this.error.set(true); this.loading.set(false); } });
    this.admin.getAuditLogs().subscribe({ next: (items) => this.audits.set(items), error: () => { this.error.set(true); this.loading.set(false); }, complete: () => this.loading.set(false) });
  }

  clearLoginHistory(): void { this.clear('login', 'Clear all login history? This cannot be undone.', () => this.admin.clearLoginHistory(), () => this.load()); }
  clearAuditLogs(): void { this.clear('audit', 'Clear all audit logs? A record of this purge will be retained.', () => this.admin.clearAuditLogs(), () => this.load()); }

  private clear(type: 'login' | 'audit', message: string, request: () => import('rxjs').Observable<void>, success: () => void): void {
    if (!this.canClearHistory() || !confirm(message)) return;
    this.clearing.set(type);
    request().subscribe({ next: success, error: () => this.error.set(true), complete: () => this.clearing.set(null) });
  }
}

