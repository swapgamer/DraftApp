import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult, Player } from './models/player.models';

export type ChemistryType = 'duo' | 'trio';
export interface ChemistryCombination { id:string; type:ChemistryType; title?:string; players:Player[]; }
export interface ChemistrySearchResult { duos:ChemistryCombination[]; trios:ChemistryCombination[]; }
export interface ChemistryUpsert { type:ChemistryType; title?:string; playerIds:string[]; }

@Injectable({ providedIn: 'root' })
export class ChemistryService {
  private readonly http=inject(HttpClient); private readonly base=`${environment.apiUrl}/chemistry`;
  browse(type:ChemistryType,pageNumber=1,pageSize=12){return this.http.get<PagedResult<ChemistryCombination>>(this.base,{params:new HttpParams().set('type',type).set('pageNumber',pageNumber).set('pageSize',pageSize)}).pipe(map(x=>({...x,items:x.items.map(item=>this.withImages(item))})));}
  search(player:string){return this.http.get<ChemistrySearchResult>(`${this.base}/search`,{params:{player}}).pipe(map(x=>({duos:x.duos.map(item=>this.withImages(item)),trios:x.trios.map(item=>this.withImages(item))})));}
  adminList(){return this.http.get<ChemistryCombination[]>(`${this.base}/admin`).pipe(map(items=>items.map(item=>this.withImages(item))));}
  create(payload:ChemistryUpsert){return this.http.post<ChemistryCombination>(this.base,payload).pipe(map(item=>this.withImages(item)));}
  update(id:string,payload:ChemistryUpsert){return this.http.put<ChemistryCombination>(`${this.base}/${id}`,payload).pipe(map(item=>this.withImages(item)));}
  delete(id:string){return this.http.delete<void>(`${this.base}/${id}`);}
  private withImages(item:ChemistryCombination):ChemistryCombination{return {...item,players:item.players.map(player=>({...player,primaryImageUrl:player.primaryImageUrl?.startsWith('/')?`${environment.apiUrl.replace('/api/v1','')}${player.primaryImageUrl}`:player.primaryImageUrl}))};}
}
