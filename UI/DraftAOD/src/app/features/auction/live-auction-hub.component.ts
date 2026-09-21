import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AuctionService, AuctionState } from '../../core/auction.service';
import { LiveAuctionLot, LiveAuctionService, LiveAuctionState, LiveAuctionTeam } from '../../core/live-auction.service';
import { AuthService } from '../../core/auth/auth.service';
import { catchError, forkJoin, map, Observable, of } from 'rxjs';

interface LoadResult<T> {
  value: T | null;
  error: unknown | null;
}

@Component({selector:'app-live-auction-hub',standalone:true,imports:[FormsModule,RouterLink,MatButtonModule,MatIconModule,MatInputModule,MatSelectModule,CurrencyPipe,DatePipe],templateUrl:'./live-auction-hub.component.html',styleUrl:'./live-auction-hub.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class LiveAuctionHubComponent implements OnInit, OnDestroy {
  private readonly live=inject(LiveAuctionService); private readonly auction=inject(AuctionService); readonly auth=inject(AuthService);
  readonly state=signal<LiveAuctionState|null>(null); readonly auctionState=signal<AuctionState|null>(null); readonly liveTeams=signal<LiveAuctionTeam[]>([]); readonly bidderChoices=signal<Record<string,string>>({}); readonly loading=signal(true); readonly error=signal(''); readonly seatKind=signal<'Admin'|'Bidder'|'Viewer'|null>(null); readonly joined=signal(false); readonly now=signal(Date.now()); readonly clockOffsetMs=signal(0); private expiryRefreshKey="";
  readonly chosenPlayerId=signal(''); readonly startingPrice=signal(1000); readonly duration=signal(60);
  private connectionId=''; private refreshTimer?:ReturnType<typeof setInterval>; private teamRefreshTimer?:ReturnType<typeof setInterval>; private clockTimer?:ReturnType<typeof setInterval>; private heartbeatTimer?:ReturnType<typeof setInterval>; private joinedAuctionId=''; private joinedTeamId?:string; private joining=false;
  readonly currentLot=computed(()=>this.state()?.currentLot??null);
  readonly myLiveTeam=computed(()=>this.liveTeams().find(team=>team.liveBidderUserId===this.auth.user()?.userId)??null);
  readonly myTeam=computed(()=>{const liveTeam=this.myLiveTeam();return liveTeam?this.auctionState()?.teams.find(team=>team.id===liveTeam.id)??null:null;});
  readonly availablePlayers=computed(()=>{const lot=this.currentLot();return (this.auctionState()?.availablePlayers??[]).filter(p=>p.id!==lot?.player.id);});
  readonly iAmLeading=computed(()=>{const lot=this.currentLot(),team=this.myTeam();return !!lot&&!!team&&lot.highestBidAuctionTeamId===team.id;});
  readonly secondsRemaining=computed(()=>{const lot=this.currentLot();if(!lot)return 0;if(lot.state==='Paused')return lot.pausedRemainingSeconds??0;const ends=lot.endsAtUtc;if(!ends)return 0;return Math.max(0,Math.ceil((new Date(ends).getTime()-(this.now()+this.clockOffsetMs()))/1000));});
  ngOnInit():void { this.connectionId=this.getConnectionId(); this.load(); this.refreshTimer=setInterval(()=>this.load(false),3000); this.teamRefreshTimer=setInterval(()=>this.loadTeams(),10000); this.clockTimer=setInterval(()=>{this.now.set(Date.now());this.refreshWhenExpired();},1000); this.heartbeatTimer=setInterval(()=>this.heartbeat(),45000); }
  ngOnDestroy():void { [this.refreshTimer,this.teamRefreshTimer,this.clockTimer,this.heartbeatTimer].forEach(timer=>timer&&clearInterval(timer)); if(this.joined())this.live.leave(this.connectionId).subscribe({error:()=>undefined}); }
  private safeLoad<T>(source: Observable<T>): Observable<LoadResult<T>> {
    return source.pipe(
      map(value => ({ value, error: null })),
      catchError(error => of({ value: null, error }))
    );
  }

  load(showLoading=true):void {
    if(showLoading)this.loading.set(true);
    const sentAt=Date.now();
    forkJoin({
      live:this.safeLoad(this.live.state()),
      auction:this.safeLoad(this.auction.state()),
      teams:this.safeLoad(this.live.teams())
    }).subscribe(result=>{
      if(result.live.value){this.syncClock(result.live.value.serverNowUtc,sentAt);this.state.set(result.live.value);this.joinSeat(result.live.value);}
      if(result.auction.value)this.auctionState.set(result.auction.value);
      if(result.teams.value)this.setTeams(result.teams.value);

      const firstError=result.live.error??result.auction.error??result.teams.error;
      this.error.set(firstError?this.errorText(firstError,'Some live auction data could not be loaded. Please retry.'):'');
      if(showLoading)this.loading.set(false);
    });
  }
  private syncClock(serverNowUtc:string|undefined,sentAt:number):void {if(!serverNowUtc)return;const serverNow=new Date(serverNowUtc).getTime();if(!Number.isFinite(serverNow))return;const receivedAt=Date.now();this.clockOffsetMs.set(serverNow-(sentAt+receivedAt)/2);}
  private refreshWhenExpired():void {const lot=this.currentLot();if(!lot||lot.state!=='Open'||this.secondsRemaining()>0)return;const key=`${lot.id}:${lot.endsAtUtc}`;if(key===this.expiryRefreshKey)return;this.expiryRefreshKey=key;setTimeout(()=>this.load(false),800);}
  loadTeams():void {this.live.teams().subscribe({next:teams=>{this.setTeams(teams);const state=this.state();if(state)this.joinSeat(state);},error:()=>undefined});}
  createLot():void {const playerId=this.chosenPlayerId();if(!playerId||this.startingPrice()<=0){this.error.set('Choose a player and enter a positive opening price.');return;}this.live.createLot(playerId,this.startingPrice()).subscribe({next:lot=>{this.state.update(s=>s?{...s,currentLot:lot}:s);this.error.set('');},error:e=>this.error.set(this.errorText(e,'The live lot could not be created.'))});}
  open():void {const lot=this.currentLot();if(lot)this.live.open(lot.id,this.duration()).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'The lot could not be opened.'))});}
  pause():void {const lot=this.currentLot();if(lot)this.live.pause(lot.id).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'The lot could not be paused.'))});}
  resume():void {const lot=this.currentLot();if(lot)this.live.resume(lot.id).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'The lot could not be resumed.'))});}
  close():void {const lot=this.currentLot();if(lot)this.live.close(lot.id).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'The lot could not be closed.'))});}
  cancel():void {const lot=this.currentLot();if(lot)this.live.cancel(lot.id).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'The lot could not be cancelled.'))});}
  bid():void {const lot=this.currentLot(),team=this.myTeam(),amount=this.requiredBid(lot);if(this.seatKind()!=='Bidder'||!lot||!team||amount<=0){this.error.set('A valid live bid is not available yet.');return;}this.live.bid(lot.id,team.id,amount).subscribe({next:()=>this.load(false),error:e=>this.error.set(this.errorText(e,'Your bid could not be accepted.'))});}
  bidderChoice(team:LiveAuctionTeam):string {return this.bidderChoices()[team.id]??team.liveBidderUserId??'';}
  chooseBidder(teamId:string,userId:string):void {this.bidderChoices.update(choices=>({...choices,[teamId]:userId}));}
  saveBidder(team:LiveAuctionTeam):void {const userId=this.bidderChoice(team)||null;this.live.setLiveBidder(team.id,userId).subscribe({next:updated=>{this.setTeams(this.liveTeams().map(item=>item.id===updated.id?updated:item));this.error.set('');this.load(false);},error:error=>this.error.set(this.errorText(error,'The live representative could not be updated.'))});}
  requiredBid(lot:LiveAuctionLot|null):number {if(!lot)return 0;const current=lot.currentBidAmount;if(current==null)return Number(lot.startingPrice)||0;return current<50?current+5:current<100?current+10:current<200?current+15:current<300?current+20:current<500?current+30:current+50;}
  position(code:string):string {const c=code.toUpperCase();return c==='GK'?'GK':['RB','LB','CB','CBST','CBSW','DEF'].includes(c)?'DEF':['DM','CMDLP','CMB2B','CAM','CM','CDM','MID'].includes(c)?'MID':'FWD';}
  private joinSeat(state:LiveAuctionState):void {const teamId=this.auth.isAdmin()?undefined:this.myLiveTeam()?.id;if((this.joinedAuctionId===state.auctionId&&this.joinedTeamId===teamId)||this.joining)return;this.joining=true;const claim=()=>this.live.join(this.connectionId,teamId).subscribe({next:seat=>{this.joining=false;this.joined.set(true);this.joinedAuctionId=state.auctionId;this.joinedTeamId=teamId;this.seatKind.set(seat.seatKind);},error:e=>{this.joining=false;this.joined.set(false);this.joinedAuctionId='';this.joinedTeamId=undefined;this.seatKind.set(null);this.error.set(this.errorText(e,'The live room is full. Please try again later.'));}});if(this.joined())this.live.leave(this.connectionId).subscribe({next:claim,error:claim});else claim();}
  private setTeams(teams:LiveAuctionTeam[]):void {this.liveTeams.set(teams);this.bidderChoices.update(choices=>Object.fromEntries(teams.map(team=>[team.id,choices[team.id]??team.liveBidderUserId??''])));}
  private heartbeat():void {
    if (!this.joined()) return;

    this.live.heartbeat(this.connectionId).subscribe({
      error: () => {
        // The free App Service can restart or wake from an idle period. Clear
        // the local seat state so the next refresh claims a fresh seat.
        this.joined.set(false);
        this.joinedAuctionId = '';
        this.joinedTeamId = undefined;
        this.seatKind.set(null);
      }
    });
  }
  private getConnectionId():string {const key='football-mayhem.live-auction.connection';let value=sessionStorage.getItem(key);if(!value){value=crypto.randomUUID?.()??`${Date.now()}-${Math.random()}`;sessionStorage.setItem(key,value);}return value;}
  private errorText(error:any,fallback:string):string {const data=error?.error;return typeof data==='string'&&data?data:typeof data?.detail==='string'?data.detail:typeof data?.title==='string'?data.title:fallback;}
}
