import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ChemistryCombination } from '../../core/chemistry.service';
import { PlayerCardComponent } from '../../shared/player-card/player-card.component';
@Component({selector:'app-chemistry-combination',standalone:true,imports:[PlayerCardComponent],template:`<article class="combination"><p class="kind">{{item().type}} chemistry</p><h3>{{ displayName() }}</h3><div class="players" [class.trio]="item().type==='trio'">@for(player of item().players;track player.id){<app-player-card [player]="player" [compact]="true"/>}</div></article>`,styleUrl:'./chemistry.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class ChemistryCombinationComponent { readonly item=input.required<ChemistryCombination>(); displayName():string{return this.item().title||this.item().players.map(player=>player.fullName).join(' + ');} }
