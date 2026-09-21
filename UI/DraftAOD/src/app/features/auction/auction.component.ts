import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AuctionPlayer, AuctionService, AuctionState, AuctionTeam, AuctionUser } from '../../core/auction.service';
import { AuthService } from '../../core/auth/auth.service';
import { ConfirmationService } from '../../core/confirmation.service';
import { NotificationService } from '../../core/notification.service';
import { LiveAuctionHubComponent } from './live-auction-hub.component';
import { finalize } from 'rxjs';

@Component({standalone:true,imports:[FormsModule,RouterLink,MatButtonModule,MatIconModule,MatInputModule,MatSelectModule,CurrencyPipe,LiveAuctionHubComponent],templateUrl:'./auction.component.html',styleUrl:'./auction.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class AuctionComponent implements OnDestroy {
  private pollTimer?:ReturnType<typeof setInterval>; private refreshing=false;
  private readonly auction=inject(AuctionService); private readonly confirmation=inject(ConfirmationService); private readonly notifications=inject(NotificationService); private readonly route=inject(ActivatedRoute); readonly auth=inject(AuthService);
  readonly state=signal<AuctionState|null>(null); readonly loading=signal(true); readonly resetting=signal(false); readonly error=signal(''); readonly tab=signal<'participants'|'sheet'|'hub'>('participants'); readonly selectedTeamId=signal<string|null>(null); readonly teamFormOpen=signal(false); readonly users=signal<AuctionUser[]>([]); readonly pending=signal<AuctionTeam[]>([]); readonly dragging=signal<AuctionPlayer|null>(null);
  readonly teamName=signal(''); readonly icon=signal('⚽'); readonly selectedMembers=signal<string[]>([]); readonly playerSearch=signal(''); readonly positionFilter=signal('ALL'); readonly prices=signal<Record<string,number>>({}); readonly approvalBalance=signal<Record<string,number>>({});
  readonly teamIcons=['⚽','🏆','🦁','🦅','🐯','🐺','🛡️','🔥','⭐','👑'];
  readonly assignmentTargets=signal<Record<string,string>>({});
  readonly selectedTeam=computed(()=>this.state()?.teams.find(team=>team.id===this.selectedTeamId())??this.state()?.teams[0]??null);
  readonly filteredPool=computed(()=>{const term=this.playerSearch().trim().toLowerCase();const position=this.positionFilter();return (this.state()?.availablePlayers??[]).filter(player=>(!term||player.fullName.toLowerCase().includes(term)||player.nationality.toLowerCase().includes(term))&&(position==='ALL'||this.category(player)===position));});
  readonly totalRemaining=computed(()=>this.state()?.teams.reduce((sum,team)=>sum+team.remainingBalance,0)??0);
  constructor(){this.route.queryParamMap.subscribe(params=>{if(['participants','sheet','hub'].includes(params.get('tab')||''))this.tab.set(params.get('tab') as 'participants'|'sheet'|'hub');if(params.get('team'))this.selectedTeamId.set(params.get('team'));});this.load();this.pollTimer=setInterval(()=>this.refreshQuietly(),3000);}
  ngOnDestroy():void { if(this.pollTimer)clearInterval(this.pollTimer); }
  // Keeps Participants and Auction Sheet current without a spinner or a page refresh. The Hub tab polls for itself.
  private refreshQuietly():void { if(document.visibilityState==='hidden'||this.tab()==='hub'||this.loading()||this.refreshing||this.dragging())return;this.refreshing=true;this.auction.state().pipe(finalize(()=>this.refreshing=false)).subscribe({next:state=>{this.state.set(state);const prices={...this.prices()};state.availablePlayers.forEach(player=>prices[player.id]=prices[player.id]??1_000_000);this.prices.set(prices);if(this.auth.isAdmin())this.loadPending();},error:()=>undefined}); }
  load():void { this.loading.set(true);this.error.set('');this.auction.state().pipe(finalize(()=>this.loading.set(false))).subscribe({next:state=>{this.state.set(state);if(!this.selectedTeamId()&&state.teams[0])this.selectedTeamId.set(state.teams[0].id);const prices:Record<string,number>={};state.availablePlayers.forEach(player=>prices[player.id]=prices[player.id]??1_000_000);this.prices.set(prices);if(this.auth.isAdmin())this.loadPending();},error:error=>this.error.set(this.errorText(error,'We could not load the live auction.'))}); }
  loadPending():void { this.auction.pendingRequests().subscribe({next:items=>this.pending.set(items)}); }
  openRequest():void { this.teamFormOpen.set(true); if(!this.users().length)this.auction.users().subscribe({next:users=>this.users.set(users.filter(user=>user.id!==this.auth.user()?.userId))}); }
  submitRequest():void {const name=this.teamName().trim();if(!name){this.error.set('Enter a team name before submitting your request.');return;}this.error.set('');this.auction.requestTeam({teamName:name,icon:this.icon().trim()||undefined,memberUserIds:this.selectedMembers()}).subscribe({next:()=>{this.teamFormOpen.set(false);this.teamName.set('');this.icon.set('⚽');this.selectedMembers.set([]);this.notifications.showSuccess('Team request submitted. It is now waiting for Admin approval.');this.load();},error:error=>this.error.set(this.errorText(error,'We could not submit the team request.'))});}
  approve(team:AuctionTeam):void {const balance=this.approvalBalance()[team.id]??100_000_000;this.auction.approve(team.id,balance).subscribe({next:()=>{this.loadPending();this.load();},error:error=>this.error.set(this.errorText(error,'We could not approve that team.'))});}
  reject(team:AuctionTeam):void {this.auction.reject(team.id).subscribe({next:()=>this.loadPending(),error:error=>this.error.set(this.errorText(error,'We could not reject that request.'))});}
  setBalance(team:AuctionTeam,value:string):void {const balance=Number(value);if(!Number.isFinite(balance)||balance<0)return;this.auction.setBalance(team.id,balance).subscribe({next:()=>this.load(),error:error=>this.error.set(this.errorText(error,'Balance could not be updated.'))});}
  dragStart(player:AuctionPlayer):void {this.dragging.set(player);} dragEnd():void {this.dragging.set(null);}
  drop(team:AuctionTeam):void {const player=this.dragging();if(!player||!this.auth.isAdmin())return;const price=this.prices()[player.id]??0;this.assign(team,player,price);this.dragging.set(null);}
  assign(team:AuctionTeam,player:AuctionPlayer,price:number):void {if(price<=0){this.error.set('Enter a positive sold price before assigning a player.');return;}this.error.set('');this.auction.assign(team.id,player.id,price).subscribe({next:()=>this.load(),error:error=>this.error.set(this.errorText(error,'The player could not be assigned.'))});}
  setAssignmentTarget(playerId:string,teamId:string):void {this.assignmentTargets.update(targets=>({...targets,[playerId]:teamId}));}
  assignToSelectedTeam(player:AuctionPlayer):void {const teamId=this.assignmentTargets()[player.id];const team=this.state()?.teams.find(item=>item.id===teamId);if(!team){this.error.set('Choose a team before assigning this player.');return;}this.assign(team,player,this.prices()[player.id]??0);}
  remove(assignmentId:string):void {this.auction.removeAssignment(assignmentId).subscribe({next:()=>this.load(),error:()=>this.error.set('The player could not be returned to the auction pool.')});}
  reset():void {this.confirmation.ask('Reset auction','This removes every auction team, live bid, live lot, and player assignment. The Football Mayhem player catalogue will not be affected.','Reset auction').subscribe(confirmed=>{if(!confirmed)return;this.error.set('');this.resetting.set(true);this.auction.reset().pipe(finalize(()=>this.resetting.set(false))).subscribe({next:()=>{this.selectedTeamId.set(null);this.pending.set([]);this.state.set(null);this.notifications.showSuccess('Auction reset successfully.');this.load();},error:error=>{const message=this.errorText(error,'The auction could not be reset.');this.error.set(message);this.notifications.showError(message);}});});}
  setPrice(playerId:string,value:string):void {const price=Number(value);this.prices.update(prices=>({...prices,[playerId]:Number.isFinite(price)?price:0}));}
  setApprovalBalance(teamId:string,value:string):void {const amount=Number(value);this.approvalBalance.update(values=>({...values,[teamId]:Number.isFinite(amount)?amount:0}));}
  toggleMember(id:string,checked:boolean):void {this.selectedMembers.update(ids=>checked?[...ids,id]:ids.filter(item=>item!==id));}
  category(player:AuctionPlayer):'GK'|'DEF'|'MID'|'FWD' {
    const code=player.primaryPositionCode.toUpperCase();
    if(code==='GK')return 'GK';
    if(['RB','LB','CB','CBST','CBSW','DEF'].includes(code))return 'DEF';
    if(['DM','CMDLP','CMB2B','CAM','CM','CDM','MID'].includes(code))return 'MID';
    return 'FWD';
  }
  positions(team:AuctionTeam,category:string):number{return team.assignments.filter(item=>this.category(item.player)===category).length;}
  memberNames(team:AuctionTeam):string{return team.members.map(member=>member.displayName).join(', ');}
  private errorText(error:any,fallback:string):string {const payload=error?.error;if(typeof payload==='string'&&payload.trim())return payload;if(typeof payload?.detail==='string'&&payload.detail.trim())return payload.detail;if(typeof payload?.title==='string'&&payload.title.trim())return payload.title;return fallback;}
}
