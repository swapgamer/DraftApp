import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { PlayerService } from '../../core/services.player';
import { SkeletonComponent } from '../../shared/skeleton/skeleton.component';
import { PlayerCardComponent } from '../../shared/player-card/player-card.component';
import { LookupsService, Lookup } from '../../core/lookups.service';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatIconModule, MatInputModule, MatPaginatorModule, MatSelectModule, SkeletonComponent, PlayerCardComponent],
  templateUrl: './hall-of-fame.component.html',
  styleUrl: './hall-of-fame.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HallOfFameComponent {
  readonly api = inject(PlayerService);
  readonly search = signal('');
  readonly rank = signal<number | null>(null);
  readonly sortBy = signal('rank');
  readonly viewMode = signal<'grid' | 'table'>('grid');
  readonly pageIndex = signal(0);
  readonly pageSize = signal(12);
  readonly positionId = signal<number | null>(null); readonly nationalityId = signal<number | null>(null); readonly playingEraId = signal<number | null>(null);
  readonly positions = signal<Lookup[]>([]); readonly nationalities = signal<Lookup[]>([]); readonly eraBuckets = signal<Lookup[]>([]);
  private readonly lookups = inject(LookupsService);

  constructor() { this.load(); this.lookups.positions().subscribe(x=>this.positions.set(x)); this.lookups.nationalities().subscribe(x=>this.nationalities.set(x)); this.lookups.eraBuckets().subscribe(x=>this.eraBuckets.set(x)); }

  load(): void {
    const selectedEra = this.eraBuckets().find(x => x.id === this.playingEraId());
    this.api.load({ pageNumber: this.pageIndex() + 1, pageSize: this.pageSize(), search: this.search().trim() || undefined, rank: this.rank() ?? undefined, positionId:this.positionId()??undefined,nationalityId:this.nationalityId()??undefined,activeFromYear:selectedEra?.startYear,activeToYear:selectedEra?.endYear, sortBy: this.sortBy(), descending: this.sortBy() !== 'rank' });
  }
  applyFilters(): void { this.pageIndex.set(0); this.load(); }
  resetFilters(): void { this.search.set(''); this.rank.set(null); this.positionId.set(null);this.nationalityId.set(null);this.playingEraId.set(null); this.sortBy.set('rank'); this.pageIndex.set(0); this.load(); }
  setViewMode(mode: 'grid' | 'table'): void { this.viewMode.set(mode); }
  changePage(event: PageEvent): void { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.load(); }
}
