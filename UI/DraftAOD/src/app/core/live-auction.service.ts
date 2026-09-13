import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LiveAuctionPlayer { id:string; fullName:string; nationality:string; positions:string[]; primaryPositionCode:string; overallRank:number; primaryImageUrl?:string; }
export interface LiveAuctionBid { id:string; auctionTeamId:string; teamName:string; amount:number; placedAtUtc:string; }
export interface LiveAuctionLot { id:string; player:LiveAuctionPlayer; startingPrice:number; currentBidAmount:number|null; state:'Draft'|'Open'|'Paused'|'Closed'|'Cancelled'; endsAtUtc:string|null; pausedRemainingSeconds:number|null; closedAtUtc:string|null; extensionCount:number; highestBidAuctionTeamId:string|null; highestBidTeamName:string|null; recentBids:LiveAuctionBid[]; }
export interface LiveAuctionCapacity { maxBidders:number; maxViewers:number; maxAdmins:number; maxConnections:number; activeBidders:number; activeViewers:number; activeAdmins:number; activeConnections:number; }
export interface LiveAuctionState { auctionId:string; currentLot:LiveAuctionLot|null; capacity:LiveAuctionCapacity; }
export interface LiveAuctionSeat { id:string; seatKind:'Bidder'|'Viewer'|'Admin'; auctionTeamId:string|null; lastSeenAtUtc:string; }
export interface LiveAuctionTeamMember { id:string; displayName:string; email:string; }
export interface LiveAuctionTeam { id:string; teamName:string; icon?:string|null; representativeUserId:string; representativeName:string; liveBidderUserId?:string|null; liveBidderName?:string|null; members:LiveAuctionTeamMember[]; }

@Injectable({providedIn:'root'})
export class LiveAuctionService {
  private readonly http=inject(HttpClient); private readonly base=`${environment.apiUrl}/auction/live`; private readonly host=environment.apiUrl.replace('/api/v1','');
  state(){return this.http.get<LiveAuctionState>(this.base).pipe(map(state=>({...state,currentLot:state.currentLot?{...state.currentLot,player:this.playerImage(state.currentLot.player)}:null})));}
  teams(){return this.http.get<LiveAuctionTeam[]>(`${this.base}/teams`);}
  setLiveBidder(teamId:string,userId:string|null){return this.http.put<LiveAuctionTeam>(`${this.base}/admin/teams/${teamId}/live-bidder`,{userId});}
  createLot(playerId:string,startingPrice:number){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots`,{playerId,startingPrice}).pipe(map(lot=>this.lotImage(lot)));}
  open(lotId:string,durationSeconds:number){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots/${lotId}/open`,{durationSeconds}).pipe(map(lot=>this.lotImage(lot)));}
  pause(lotId:string){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots/${lotId}/pause`,{}).pipe(map(lot=>this.lotImage(lot)));}
  resume(lotId:string){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots/${lotId}/resume`,{}).pipe(map(lot=>this.lotImage(lot)));}
  close(lotId:string){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots/${lotId}/close`,{}).pipe(map(lot=>this.lotImage(lot)));}
  cancel(lotId:string){return this.http.post<LiveAuctionLot>(`${this.base}/admin/lots/${lotId}/cancel`,{}).pipe(map(lot=>this.lotImage(lot)));}
  bid(lotId:string,auctionTeamId:string,amount:number){return this.http.post<LiveAuctionLot>(`${this.base}/lots/${lotId}/bids`,{auctionTeamId,amount}).pipe(map(lot=>this.lotImage(lot)));}
  join(connectionId:string,auctionTeamId?:string){return this.http.post<LiveAuctionSeat>(`${this.base}/seats/join`,{connectionId,auctionTeamId:auctionTeamId||null});}
  heartbeat(connectionId:string){return this.http.post<void>(`${this.base}/seats/heartbeat`,{connectionId});}
  leave(connectionId:string){return this.http.delete<void>(`${this.base}/seats/${encodeURIComponent(connectionId)}`);}
  private lotImage(lot:LiveAuctionLot):LiveAuctionLot{return {...lot,player:this.playerImage(lot.player)};}
  private playerImage(player:LiveAuctionPlayer):LiveAuctionPlayer{return {...player,primaryImageUrl:player.primaryImageUrl?.startsWith('/')?`${this.host}${player.primaryImageUrl}`:player.primaryImageUrl};}
}
