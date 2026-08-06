import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FavoritesService } from '../../core/favorites.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
@Component({ standalone:true, imports:[RouterLink,MatButtonModule,MatIconModule,EmptyStateComponent], template:`<section class="page"><h1>Your favorites</h1>@if (favorites.favorites().length) { <div class="list">@for (player of favorites.favorites();track player.playerId){<a [routerLink]="['/players',player.playerId]"><strong>#{{player.overallRank}} {{player.fullName}}</strong><span>{{player.positions.join(' · ')}} · {{player.nationality}} · {{player.playingEra}}</span></a>}</div>} @else {<app-empty-state icon="favorite_border" title="No favorites yet" description="Use the heart on a player profile to save players here."/>}</section>`,styles:`.page{max-width:62rem;margin:auto;padding:3rem 1.5rem}h1{color:var(--text-strong)}.list{display:grid;gap:.7rem}.list a{display:grid;gap:.3rem;padding:1rem;color:var(--text-soft);text-decoration:none;background:var(--surface);border:1px solid var(--border);border-radius:.75rem}.list strong{color:var(--text-strong)}.list span{color:var(--muted);font-size:.9rem}` ,changeDetection:ChangeDetectionStrategy.OnPush})
export class FavoritesComponent { readonly favorites=inject(FavoritesService); constructor(){this.favorites.load();} }
