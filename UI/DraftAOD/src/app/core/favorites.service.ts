import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
export interface FavoritePlayer { playerId: string; fullName: string; overallRank: number; nationality: string; playingEra: string; positions: string[]; }
@Injectable({ providedIn: 'root' }) export class FavoritesService {
  private readonly http = inject(HttpClient); readonly favorites = signal<FavoritePlayer[]>([]); readonly loaded = signal(false);
  load(): void { this.http.get<FavoritePlayer[]>(`${environment.apiUrl}/favorites`).subscribe({ next: x => { this.favorites.set(x); this.loaded.set(true); }, error: () => this.loaded.set(true) }); }
  has(id: string): boolean { return this.favorites().some(x => x.playerId === id); }
  toggle(id: string): void { const exists = this.has(id); this.http.request<void>(exists ? 'DELETE' : 'POST', `${environment.apiUrl}/favorites/${id}`).subscribe({ next: () => exists ? this.favorites.update(x => x.filter(f => f.playerId !== id)) : this.load() }); }
}
