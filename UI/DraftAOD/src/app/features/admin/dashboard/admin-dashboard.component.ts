import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AdminService, DashboardMetrics } from '../../../core/admin/admin.service';

@Component({
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDashboardComponent {
  readonly metrics = signal<DashboardMetrics | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);

  private readonly admin = inject(AdminService);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.admin.getDashboard().subscribe({
      next: (metrics) => this.metrics.set(metrics),
      error: () => this.error.set(true),
      complete: () => this.loading.set(false),
    });
  }
}
