import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AdminService, AdminUser } from '../../../core/admin/admin.service';
import { ConfirmationService } from '../../../core/confirmation.service';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatIconModule, MatSelectModule, MatTableModule],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUsersComponent {
  readonly users = signal<AdminUser[]>([]);
  readonly searchTerm = signal('');
  readonly filteredUsers = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    return term ? this.users().filter(user => `${user.displayName} ${user.email}`.toLowerCase().includes(term)) : this.users();
  });
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly updatingId = signal<string | null>(null);
  readonly columns = ['user', 'roles', 'created', 'status', 'actions'];
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);
  private readonly confirmation = inject(ConfirmationService);
  readonly canManageAdmins = computed(() => this.auth.user()?.isSystemAdmin ?? false);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set(false);
    this.admin.getUsers().subscribe({ next: users => this.users.set(users), error: () => { this.error.set(true); this.loading.set(false); }, complete: () => this.loading.set(false) });
  }

  grantTemporaryAdmin(user: AdminUser): void {
    const expiresAtUtc = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();
    this.confirmation.ask('Add temporary admin', `Grant ${user.displayName} admin access for 30 days? They cannot manage administrator access.`, 'Add admin').subscribe(confirmed => {
      if (!confirmed) return;
      this.updatingId.set(user.id);
      this.admin.grantTemporaryAdmin(user.id, expiresAtUtc).subscribe({ next: () => this.users.update(items => items.map(item => item.id === user.id ? { ...item, roles: ['Admin'], adminExpiresAtUtc: expiresAtUtc } : item)), error: () => this.error.set(true), complete: () => this.updatingId.set(null) });
    });
  }

  revokeTemporaryAdmin(user: AdminUser): void {
    this.confirmation.ask('Remove temporary admin', `Remove admin access from ${user.displayName}?`, 'Remove admin').subscribe(confirmed => {
      if (!confirmed) return;
      this.updatingId.set(user.id);
      this.admin.revokeTemporaryAdmin(user.id).subscribe({ next: () => this.users.update(items => items.map(item => item.id === user.id ? { ...item, roles: ['User'], adminExpiresAtUtc: undefined } : item)), error: () => this.error.set(true), complete: () => this.updatingId.set(null) });
    });
  }

  changeStatus(user: AdminUser): void {
    const active = !user.isActive;
    const action = active ? 'Reactivate' : 'Deactivate';
    this.confirmation.ask(`${action} account`, `Do you want to ${action.toLowerCase()} ${user.displayName}'s account?`, action).subscribe(confirmed => {
      if (!confirmed) return;
      this.updatingId.set(user.id);
      this.admin.setUserStatus(user.id, active).subscribe({ next: () => this.users.update(items => items.map(item => item.id === user.id ? { ...item, isActive: active } : item)), error: () => this.error.set(true), complete: () => this.updatingId.set(null) });
    });
  }
}
