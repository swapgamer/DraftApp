import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { PagedResult, Player } from '../../core/models/player.models';
import { PlayerService } from '../../core/services.player';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';

@Component({ standalone: true, imports: [EmptyStateComponent, FormsModule, RouterLink, MatButtonModule, MatIconModule, MatInputModule], templateUrl: './search.component.html', styleUrl: './search.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class SearchComponent {
  readonly query = signal('');
  readonly results = signal<Player[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly searched = signal(false);
  private readonly terms = new Subject<string>();
  private readonly players = inject(PlayerService);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.terms.pipe(debounceTime(300), distinctUntilChanged(), tap((term) => { this.loading.set(Boolean(term)); this.searched.set(Boolean(term)); }), switchMap((term): Observable<PagedResult<Player>> => term ? this.players.search({ pageNumber: 1, pageSize: 12, search: term, sortBy: 'rank' }).pipe(catchError(() => of({ items: [], pageNumber: 1, pageSize: 12, totalCount: 0 }))) : of({ items: [], pageNumber: 1, pageSize: 12, totalCount: 0 })), takeUntilDestroyed(this.destroyRef)).subscribe((result) => { this.results.set(result.items); this.total.set(result.totalCount); this.loading.set(false); });
  }

  update(term: string): void { this.query.set(term); this.terms.next(term.trim()); }
  useSuggestion(term: string): void { this.update(term); }
  highlight(value: string): string {
    const term = this.query().trim();
    const escaped = value.replace(/[&<>"']/g, (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character] ?? character);
    if (!term) return escaped;
    const safeTerm = term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return escaped.replace(new RegExp(`(${safeTerm})`, 'ig'), '<mark>$1</mark>');
  }
}
