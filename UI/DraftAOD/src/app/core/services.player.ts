import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult, Player, PlayerSearch } from './models/player.models';

export interface PlayerUpsert { fullName: string; nationalityId: number; playingEraId: number; shortDescription: string; overallRank: number; goalCreditPoints: number; assistCreditPoints: number; defensiveCreditPoints: number; transfermarktUrl?: string; wikipediaUrl?: string; aliases: string[]; positionIds: number[]; primaryPositionId: number; }
export interface PlayerPatch { fullName?: string; nationalityId?: number; playingEraId?: number; shortDescription?: string; overallRank?: number; goalCreditPoints?: number; assistCreditPoints?: number; defensiveCreditPoints?: number; transfermarktUrl?: string; wikipediaUrl?: string; aliases?: string[]; positionIds?: number[]; primaryPositionId?: number; }

@Injectable({ providedIn: 'root' })
export class PlayerService {
  readonly players = signal<Player[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  constructor(private readonly http: HttpClient) {}

  getById(id: string) { return this.http.get<Player>(`${environment.apiUrl}/players/${id}`).pipe(map(player => this.withImageUrl(player))); }

  search(query: PlayerSearch) {
    let params = new HttpParams();
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    });
    return this.http.get<PagedResult<Player>>(`${environment.apiUrl}/players`, { params }).pipe(map(result => ({ ...result, items: result.items.map(player => this.withImageUrl(player)) })));
  }

  load(query: PlayerSearch): void {
    this.loading.set(true);
    this.search(query).subscribe({
      next: (result) => { this.players.set(result.items); this.total.set(result.totalCount); },
      error: () => this.loading.set(false),
      complete: () => this.loading.set(false),
    });
  }

  create(payload: PlayerUpsert) { return this.http.post<Player>(`${environment.apiUrl}/players`, payload).pipe(map(player => this.withImageUrl(player))); }
  patch(id: string, payload: PlayerPatch) { return this.http.patch<Player>(`${environment.apiUrl}/players/${id}`, payload).pipe(map(player => this.withImageUrl(player))); }
  uploadImage(playerId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ imageUrl: string }>(`${environment.apiUrl}/players/${playerId}/images`, form).pipe(map(response => response.imageUrl.startsWith('/') ? `${environment.apiUrl.replace('/api/v1', '')}${response.imageUrl}` : response.imageUrl));
  }
  update(id: string, payload: PlayerUpsert) { return this.http.put<Player>(`${environment.apiUrl}/players/${id}`, payload).pipe(map(player => this.withImageUrl(player))); }
  delete(id: string) { return this.http.delete<void>(`${environment.apiUrl}/players/${id}`); }
  restore(id: string) { return this.http.post<void>(`${environment.apiUrl}/players/${id}/restore`, {}); }

  private withImageUrl(player: Player): Player {
    return player.primaryImageUrl?.startsWith('/') ? { ...player, primaryImageUrl: `${environment.apiUrl.replace('/api/v1', '')}${player.primaryImageUrl}` } : player;
  }
}
