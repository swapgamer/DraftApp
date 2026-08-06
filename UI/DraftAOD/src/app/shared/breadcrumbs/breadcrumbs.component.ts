import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

interface Breadcrumb { label: string; url: string; }

@Component({
  selector: 'app-breadcrumbs',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './breadcrumbs.component.html',
  styleUrl: './breadcrumbs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BreadcrumbsComponent {
  readonly crumbs = signal<Breadcrumb[]>([]);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.update();
    this.router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.update());
  }

  private update(): void {
    const crumbs: Breadcrumb[] = [];
    let route: ActivatedRoute | null = this.route;
    let url = '';
    while (route?.firstChild) {
      route = route.firstChild;
      const segment = route.snapshot.url.map((item) => item.path).join('/');
      if (!segment) continue;
      url += `/${segment}`;
      const title = route.snapshot.title?.toString().split('|')[0].trim();
      crumbs.push({ label: title || this.format(segment), url });
    }
    this.crumbs.set(crumbs);
  }

  private format(value: string): string { return value.replace(/[-_]/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase()); }
}

