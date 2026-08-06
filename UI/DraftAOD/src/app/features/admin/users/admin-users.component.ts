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
  private readonly confirmation = inject(ConfirmationService);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set(false);
    this.admin.getUsers().subscribe({ next: users => this.users.set(users), error: () => this.error.set(true), complete: () => this.loading.set(false) });
  }

  changeRole(user: AdminUser, roleId: number): void {
    const role = roleId === 2 ? 'Admin' : 'User';
    this.confirmation.ask('Change user role', `Assign the ${role} role to ${user.displayName}?`, 'Confirm').subscribe(confirmed => {
      if (!confirmed) return;
      this.updatingId.set(user.id);
      this.admin.setUserRole(user.id, roleId).subscribe({ next: () => this.users.update(items => items.map(item => item.id === user.id ? { ...item, roles: [role] } : item)), error: () => this.error.set(true), complete: () => this.updatingId.set(null) });
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
