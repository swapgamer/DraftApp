import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Player } from '../../core/models/player.models';
import { PlayerService } from '../../core/services.player';
import { FavoritesService } from '../../core/favorites.service';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './player-profile.component.html',
  styleUrl: './player-profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerProfileComponent {
  readonly player = signal<Player | null>(null);
  readonly loading = signal(true);
  readonly missing = signal(false);
  readonly imageSaving = signal(false);
  readonly imageMessage = signal('');

  private readonly route = inject(ActivatedRoute);
  private readonly players = inject(PlayerService);
  readonly favorites = inject(FavoritesService);
  readonly auth = inject(AuthService);

  constructor() {
    this.route.paramMap.subscribe((params) => this.loadPlayer(params.get('id')));
  }

  private loadPlayer(id: string | null): void {
    this.loading.set(true);
    this.missing.set(false);
    this.player.set(null);
    if (!this.favorites.loaded()) this.favorites.load();
    if (!id) {
      this.loading.set(false);
      this.missing.set(true);
      return;
    }

    this.players.getById(id).subscribe({
      next: (player) => this.player.set(player),
      error: () => { this.missing.set(true); this.loading.set(false); },
      complete: () => this.loading.set(false),
    });
  }

  uploadImage(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    const player = this.player();
    if (!file || !player) return;
    this.imageSaving.set(true);
    this.imageMessage.set('Uploading image...');
    this.players.uploadImage(player.id, file).subscribe({
      next: imageUrl => {
        this.player.update(current => current ? { ...current, primaryImageUrl: `${imageUrl}?v=${Date.now()}` } : current);
        this.imageMessage.set('Player image updated.');
      },
      error: error => {
        this.imageSaving.set(false);
        this.imageMessage.set(this.errorText(error));
      },
      complete: () => this.imageSaving.set(false),
    });
  }

  private errorText(error: any): string {
    const body = error?.error;
    if (typeof body === 'string') return body;
    const validation = body?.errors ? Object.values(body.errors).flat().find(value => typeof value === 'string') : undefined;
    const message = validation || body?.detail || body?.title || body?.message;
    return typeof message === 'string' ? message : 'Image upload failed. Use a JPEG, PNG, or WebP file under 5 MB.';
  }
}

