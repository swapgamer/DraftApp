import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LookupsService } from '../../../core/lookups.service';
import { Player } from '../../../core/models/player.models';
import { PlayerPatch, PlayerService, PlayerUpsert } from '../../../core/services.player';

@Component({
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <header>
        <h1>Player management</h1>
        <button mat-flat-button color="primary" (click)="startCreate()"><mat-icon>add</mat-icon>Add player</button>
      </header>
      <form class="search-form" (ngSubmit)="search()">
        <input name="search" [(ngModel)]="searchTerm" placeholder="Search by player name or alias">
        <button mat-stroked-button type="submit"><mat-icon>search</mat-icon>Search</button>
        @if (searchTerm) { <button mat-button type="button" (click)="clearSearch()">Clear</button> }
      </form>

      @if (open()) {
        <form (ngSubmit)="save()">
          <h2>{{ editingId() ? 'Edit player' : 'Add player' }}</h2>
          <input name="name" [(ngModel)]="name" placeholder="Full name" required>
          <input name="rank" [(ngModel)]="rank" type="number" min="1" placeholder="Rank" required>
          <select name="nationality" [(ngModel)]="nationality" required>
            <option [ngValue]="0">Nationality</option>
            @for (x of nationalities(); track x.id) { <option [ngValue]="x.id">{{ x.name }}</option> }
          </select>
          <select name="era" [(ngModel)]="era" required>
            <option [ngValue]="0">Era</option>
            @for (x of eras(); track x.id) { <option [ngValue]="x.id">{{ x.name }}</option> }
          </select>
          <select name="position" [(ngModel)]="position" required>
            <option [ngValue]="0">Position</option>
            @for (x of positions(); track x.id) { <option [ngValue]="x.id">{{ x.name }}</option> }
          </select>
          <input name="goalCredit" [(ngModel)]="goalCredit" type="number" min="0" placeholder="Goal credit">
          <input name="assistCredit" [(ngModel)]="assistCredit" type="number" min="0" placeholder="Assist credit">
          <input name="defenceCredit" [(ngModel)]="defenceCredit" type="number" min="0" placeholder="Defence credit">
          <input name="aliases" [(ngModel)]="aliases" placeholder="Aliases, comma separated">
          <input name="transfermarktUrl" [(ngModel)]="transfermarktUrl" placeholder="Transfermarkt URL (optional)">
          <input name="wikipediaUrl" [(ngModel)]="wikipediaUrl" placeholder="Wikipedia URL (optional)">
          <label class="file-input">{{ editingId() ? 'Replace player image (optional)' : 'Player image (optional)' }}
            <input type="file" accept="image/jpeg,image/png,image/webp" (change)="selectImage($event)">
          </label>
          @if (message()) { <p class="form-message">{{ message() }}</p> }
          <textarea name="description" [(ngModel)]="description" placeholder="Short description" required></textarea>
          <div class="form-actions">
            <button mat-flat-button color="primary" [disabled]="saving()">{{ saving() ? 'Saving...' : (editingId() ? 'Save changes' : 'Save player') }}</button>
            <button mat-button type="button" (click)="cancel()">Cancel</button>
          </div>
        </form>
      }

      <p class="result-count">{{ api.total() }} player{{ api.total() === 1 ? '' : 's' }} found</p>
      <div class="list">
        @for (p of api.players(); track p.id) {
          <article>
            <span>#{{ p.overallRank }}</span><strong>{{ p.fullName }}</strong><small>{{ p.positions.join(', ') }}</small>
            <a mat-button [routerLink]="['/players', p.id]">View</a>
            <button mat-button (click)="edit(p)">Edit</button>
            <button mat-button color="warn" (click)="remove(p.id)">Delete</button>
          </article>
        }
      </div>
    </section>`,
  styles: `.page{max-width:75rem;margin:auto;padding:3rem 1.5rem}header,form{display:flex;gap:1rem;align-items:center;justify-content:space-between;flex-wrap:wrap}h1,h2{color:var(--text-strong)}form{margin:1rem 0;padding:1rem;background:var(--surface);border:1px solid var(--border)}.search-form{justify-content:flex-start}.search-form input{min-width:18rem}form h2,textarea{width:100%}input,select,textarea{padding:.7rem;color:var(--text);background:var(--surface-deep);border:1px solid var(--border)}.file-input{color:var(--text);display:grid;gap:.4rem}.file-input input{max-width:17rem}.form-message,.result-count{width:100%;margin:.75rem 0;color:var(--accent)}textarea{min-height:5rem}.form-actions{display:flex;gap:.5rem}.list{display:grid;gap:.5rem}.list article{display:grid;grid-template-columns:4rem 1fr 1fr auto auto auto;gap:1rem;align-items:center;padding:1rem;background:var(--surface);border:1px solid var(--border)}.list span{color:var(--accent)}@media(max-width:48rem){.search-form input{min-width:0;width:100%}.list article{grid-template-columns:4rem 1fr;gap:.5rem}}`
})
export class AdminPlayersComponent {
  readonly api = inject(PlayerService);
  private readonly lookup = inject(LookupsService);
  private readonly route = inject(ActivatedRoute);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly message = signal('');
  readonly editingId = signal<string | null>(null);
  readonly positions = signal<any[]>([]);
  readonly nationalities = signal<any[]>([]);
  readonly eras = signal<any[]>([]);
  name = ''; rank = 1; nationality = 0; era = 0; position = 0; description = ''; aliases = '';
  searchTerm = '';
  goalCredit = 0; assistCredit = 0; defenceCredit = 0; transfermarktUrl = ''; wikipediaUrl = ''; image?: File;
  private pendingEditId?: string;
  private original?: { name:string; rank:number; nationality:number; era:number; position:number; description:string; aliases:string; goalCredit:number; assistCredit:number; defenceCredit:number; transfermarktUrl:string; wikipediaUrl:string; };

  constructor() {
    this.reload();
    this.lookup.positions().subscribe(x => { this.positions.set(x); this.tryPendingEdit(); });
    this.lookup.nationalities().subscribe(x => { this.nationalities.set(x); this.tryPendingEdit(); });
    this.lookup.eras().subscribe(x => { this.eras.set(x); this.tryPendingEdit(); });
    this.route.queryParamMap.subscribe(params => { this.pendingEditId = params.get('edit') ?? undefined; this.tryPendingEdit(); });
  }

  reload() { this.api.load({ pageNumber: 1, pageSize: 100, sortBy: 'rank', search: this.searchTerm.trim() || undefined }); }
  search() { this.reload(); }
  clearSearch() { this.searchTerm = ''; this.reload(); }
  startCreate() { this.reset(); this.open.set(true); }
  cancel() { this.reset(); this.open.set(false); }
  selectImage(event: Event) { this.image = (event.target as HTMLInputElement).files?.[0]; this.message.set(this.image ? `Selected image: ${this.image.name}` : ''); }

  edit(summary: Player) {
    this.api.getById(summary.id).subscribe(player => {
      this.name = player.fullName; this.rank = player.overallRank; this.description = player.shortDescription;
      this.aliases = player.aliases.join(', '); this.goalCredit = player.goalCreditPoints; this.assistCredit = player.assistCreditPoints;
      this.defenceCredit = player.defensiveCreditPoints; this.transfermarktUrl = player.transfermarktUrl ?? ''; this.wikipediaUrl = player.wikipediaUrl ?? '';
      this.nationality = this.nationalities().find(x => x.name === player.nationality)?.id ?? 0;
      this.era = this.eras().find(x => x.name === player.playingEra)?.id ?? 0;
      this.position = this.positions().find(x => x.name === player.positions[0] || x.code === player.positions[0])?.id ?? 0;
      this.original = this.snapshot(); this.image = undefined; this.editingId.set(player.id); this.open.set(true);
    });
  }

  private tryPendingEdit() {
    if (!this.pendingEditId || !this.positions().length || !this.nationalities().length || !this.eras().length) return;
    const id = this.pendingEditId;
    this.pendingEditId = undefined;
    this.edit({ id } as Player);
  }

  save() {
    const id = this.editingId();
    if (id) { this.saveChanges(id); return; }
    if (!this.name || !this.nationality || !this.era || !this.position) { this.message.set('Complete the name, rank, nationality, era, position, and description fields before saving.'); return; }
    this.saving.set(true);
    this.message.set('');
    const payload: PlayerUpsert = {
      fullName: this.name, nationalityId: this.nationality, playingEraId: this.era, shortDescription: this.description,
      overallRank: this.rank, goalCreditPoints: this.goalCredit, assistCreditPoints: this.assistCredit, defensiveCreditPoints: this.defenceCredit,
      transfermarktUrl: this.transfermarktUrl || undefined, wikipediaUrl: this.wikipediaUrl || undefined,
      aliases: this.aliases.split(',').map(x => x.trim()).filter(Boolean), positionIds: [this.position], primaryPositionId: this.position
    };
    this.api.create(payload).subscribe({ next: player => this.saveImageThenFinish(player.id), error: error => this.fail(error, 'Player details could not be saved.') });
  }

  private saveChanges(id: string) {
    const original = this.original;
    if (!original) { this.message.set('Player details are still loading.'); return; }
    const patch: PlayerPatch = {};
    if (this.name !== original.name) patch.fullName = this.name;
    if (this.rank !== original.rank) patch.overallRank = this.rank;
    if (this.nationality && this.nationality !== original.nationality) patch.nationalityId = this.nationality;
    if (this.era && this.era !== original.era) patch.playingEraId = this.era;
    if (this.position && this.position !== original.position) { patch.positionIds = [this.position]; patch.primaryPositionId = this.position; }
    if (this.description !== original.description) patch.shortDescription = this.description;
    if (this.goalCredit !== original.goalCredit) patch.goalCreditPoints = this.goalCredit;
    if (this.assistCredit !== original.assistCredit) patch.assistCreditPoints = this.assistCredit;
    if (this.defenceCredit !== original.defenceCredit) patch.defensiveCreditPoints = this.defenceCredit;
    if (this.aliases !== original.aliases) patch.aliases = this.aliases.split(',').map(x => x.trim()).filter(Boolean);
    if (this.transfermarktUrl !== original.transfermarktUrl) patch.transfermarktUrl = this.transfermarktUrl;
    if (this.wikipediaUrl !== original.wikipediaUrl) patch.wikipediaUrl = this.wikipediaUrl;
    if (!Object.keys(patch).length && !this.image) { this.message.set('No changes to save.'); return; }
    this.saving.set(true); this.message.set('');
    if (!Object.keys(patch).length) { this.saveImageThenFinish(id); return; }
    this.api.patch(id, patch).subscribe({ next: player => this.saveImageThenFinish(player.id), error: error => this.fail(error, 'Player changes could not be saved.') });
  }

  private saveImageThenFinish(playerId: string) {
    if (!this.image) { this.finishSave(); return; }
    this.api.uploadImage(playerId, this.image).subscribe({ next: () => this.finishSave(), error: error => this.fail(error, 'The player was saved, but the image upload failed.') });
  }
  private finishSave() { this.cancel(); this.saving.set(false); this.reload(); }
  private fail(error: any, fallback: string) { const body=error?.error; const validation=body?.errors?Object.values(body.errors).flat().find(value=>typeof value === 'string'):undefined; const message=validation||body?.detail||body?.title||body?.message; this.saving.set(false); this.message.set(typeof body === 'string' ? body : typeof message === 'string' ? message : fallback); }
  private snapshot() { return { name:this.name, rank:this.rank, nationality:this.nationality, era:this.era, position:this.position, description:this.description, aliases:this.aliases, goalCredit:this.goalCredit, assistCredit:this.assistCredit, defenceCredit:this.defenceCredit, transfermarktUrl:this.transfermarktUrl, wikipediaUrl:this.wikipediaUrl }; }
  private reset() { this.message.set(''); this.original=undefined; this.editingId.set(null); this.name = ''; this.rank = 1; this.nationality = 0; this.era = 0; this.position = 0; this.description = ''; this.aliases = ''; this.goalCredit = 0; this.assistCredit = 0; this.defenceCredit = 0; this.transfermarktUrl = ''; this.wikipediaUrl = ''; this.image = undefined; }
  remove(id: string) { if (confirm('Delete this player?')) this.api.delete(id).subscribe(() => this.reload()); }
}
