import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ChemistryCombination, ChemistryService, ChemistryType } from '../../../core/chemistry.service';
import { PlayerService } from '../../../core/services.player';
import { Player } from '../../../core/models/player.models';

@Component({standalone:true,imports:[FormsModule,MatButtonModule,MatFormFieldModule,MatIconModule,MatInputModule,MatSelectModule],templateUrl:'./admin-chemistry.component.html',styleUrl:'./admin-chemistry.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class AdminChemistryComponent {
  private readonly chemistry=inject(ChemistryService); private readonly playersApi=inject(PlayerService);
  readonly combinations=signal<ChemistryCombination[]>([]); readonly candidates=signal<Player[]>([]); readonly selectedPlayers=signal<Player[]>([]); readonly type=signal<ChemistryType>('duo'); readonly title=signal(''); readonly search=signal(''); readonly editingId=signal<string|null>(null); readonly error=signal(''); readonly saving=signal(false);
  constructor(){this.refresh();}
  refresh():void{this.chemistry.adminList().subscribe({next:items=>this.combinations.set(items),error:()=>this.error.set('Could not load chemistry management.')});}
  findPlayers():void{const term=this.search().trim();if(!term)return;this.playersApi.search({pageNumber:1,pageSize:8,search:term,sortBy:'name'}).subscribe(result=>this.candidates.set(result.items));}
  addPlayer(player:Player):void{if(this.selectedPlayers().some(x=>x.id===player.id)||this.selectedPlayers().length>=this.required())return;this.selectedPlayers.update(items=>[...items,player]);}
  removePlayer(id:string):void{this.selectedPlayers.update(items=>items.filter(x=>x.id!==id));}
  save():void{if(this.selectedPlayers().length!==this.required()){this.error.set(`Select exactly ${this.required()} players for this ${this.type()}.`);return;}this.saving.set(true);this.error.set('');const payload={type:this.type(),title:this.title().trim()||undefined,playerIds:this.selectedPlayers().map(x=>x.id)};const request=this.editingId()?this.chemistry.update(this.editingId()!,payload):this.chemistry.create(payload);request.subscribe({next:()=>{this.cancel();this.refresh();},error:()=>{this.error.set('Could not save the chemistry combination.');this.saving.set(false);},complete:()=>this.saving.set(false)});}
  edit(item:ChemistryCombination):void{this.editingId.set(item.id);this.type.set(item.type);this.title.set(item.title||'');this.selectedPlayers.set(item.players);this.candidates.set([]);this.error.set('');}
  cancel():void{this.editingId.set(null);this.type.set('duo');this.title.set('');this.selectedPlayers.set([]);this.candidates.set([]);this.search.set('');this.error.set('');}
  delete(item:ChemistryCombination):void{if(!confirm(`Remove ${item.title||item.players.map(x=>x.fullName).join(' + ')}?`))return;this.chemistry.delete(item.id).subscribe({next:()=>this.refresh(),error:()=>this.error.set('Could not remove the chemistry combination.')});}
  required():number{return this.type()==='duo'?2:3;}
  isSelected(playerId:string):boolean{return this.selectedPlayers().some(player=>player.id===playerId);}
  displayName(item:ChemistryCombination):string{return item.title||item.players.map(player=>player.fullName).join(' + ');}
  playerNames(item:ChemistryCombination):string{return item.players.map(player=>player.fullName).join(' + ');}
}
