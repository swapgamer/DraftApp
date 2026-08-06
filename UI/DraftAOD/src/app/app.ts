import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './core/auth/auth.service';
import { LoadingService } from './core/loading.service';
import { ThemeService } from './core/theme.service';
import { BreadcrumbsComponent } from './shared/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-root', standalone: true,
  imports: [BreadcrumbsComponent, RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatListModule, MatProgressSpinnerModule, MatSidenavModule, MatToolbarModule],
  templateUrl: './app.html', styleUrl: './app.scss', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  readonly auth = inject(AuthService);
  readonly loading = inject(LoadingService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  logout(): void { this.auth.logout().subscribe({ next: () => this.router.navigateByUrl('/'), error: () => this.router.navigateByUrl('/') }); }
}
