import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuctionUser { id:string; displayName:string; email:string; }
export interface AuctionPlayer { id:string; fullName:string; nationality:string; positions:string[]; primaryPositionCode:string; overallRank:number; primaryImageUrl?:string; }
export interface AuctionAssignment { id:string; player:AuctionPlayer; soldPrice:number; assignedAtUtc:string; }
export interface AuctionTeam { id:string; teamName:string; icon?:string; status:'Pending'|'Approved'|'Rejected'; representative:AuctionUser; members:AuctionUser[]; startingBalance:number; totalSpent:number; remainingBalance:number; assignments:AuctionAssignment[]; }
export interface AuctionState { auctionId:string; name:string; teams:AuctionTeam[]; availablePlayers:AuctionPlayer[]; }

@Injectable({providedIn:'root'})
export class AuctionService {
  private readonly http=inject(HttpClient); private readonly base=`${environment.apiUrl}/auction`; private readonly host=environment.apiUrl.replace('/api/v1','');
  state(){return this.http.get<AuctionState>(this.base).pipe(map(state=>({...state,teams:state.teams.map(team=>this.teamImages(team)),availablePlayers:state.availablePlayers.map(player=>this.playerImage(player))})));}
  users(search=''){return this.http.get<AuctionUser[]>(`${this.base}/users`,{params:{search}});}
  requestTeam(payload:{teamName:string;icon?:string;memberUserIds:string[]}){return this.http.post<AuctionTeam>(`${this.base}/team-requests`,payload).pipe(map(team=>this.teamImages(team)));}
  myRequests(){return this.http.get<AuctionTeam[]>(`${this.base}/my-team-requests`).pipe(map(teams=>teams.map(team=>this.teamImages(team))));}
  pendingRequests(){return this.http.get<AuctionTeam[]>(`${this.base}/admin/requests`).pipe(map(teams=>teams.map(team=>this.teamImages(team))));}
  approve(teamId:string,startingBalance:number){return this.http.post<AuctionTeam>(`${this.base}/admin/teams/${teamId}/approve`,{startingBalance}).pipe(map(team=>this.teamImages(team)));}
  reject(teamId:string){return this.http.post<void>(`${this.base}/admin/teams/${teamId}/reject`,{});}
  setBalance(teamId:string,startingBalance:number){return this.http.put<AuctionTeam>(`${this.base}/admin/teams/${teamId}/balance`,{startingBalance}).pipe(map(team=>this.teamImages(team)));}
  assign(teamId:string,playerId:string,soldPrice:number){return this.http.post<AuctionAssignment>(`${this.base}/admin/assignments`,{teamId,playerId,soldPrice}).pipe(map(item=>({...item,player:this.playerImage(item.player)})));}
  removeAssignment(id:string){return this.http.delete<void>(`${this.base}/admin/assignments/${id}`);}
  reset(){return this.http.delete<void>(`${this.base}/admin/reset`);}
  private teamImages(team:AuctionTeam):AuctionTeam{return {...team,assignments:team.assignments.map(item=>({...item,player:this.playerImage(item.player)}))};}
  private playerImage(player:AuctionPlayer):AuctionPlayer{return {...player,primaryImageUrl:player.primaryImageUrl?.startsWith('/')?`${this.host}${player.primaryImageUrl}`:player.primaryImageUrl};}
}
