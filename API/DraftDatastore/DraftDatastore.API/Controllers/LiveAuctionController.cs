using System.Security.Claims;
using DraftDatastore.API.LiveAuction;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.API.Controllers;

/// <summary>
/// Persists the live-auction lifecycle. SignalR/browser presence is deliberately
/// kept out of this controller so a sleeping free-tier App Service can recover
/// from the stored deadline, bid history, and room seats.
/// </summary>
[ApiController, Authorize, Route("api/v1/auction/live")]
public sealed class LiveAuctionController(DraftDatastoreDbContext db, LiveAuctionSettlement settlement) : ControllerBase
{
    private const int MaxBidders = 12;
    private const int MaxViewers = 10;
    private const int MaxAdmins = 3;
    private const int MaxConnections = MaxBidders + MaxViewers + MaxAdmins;
    private static readonly TimeSpan SeatTtl = TimeSpan.FromMinutes(2);
    // The free App Service runs one instance. Serialising seat changes here keeps
    // parallel browser tabs from racing while a seat is being claimed or replaced.
    private static readonly SemaphoreSlim SeatGate = new(1, 1);

    [HttpGet]
    public async Task<ActionResult<LiveAuctionStateResponse>> Get(CancellationToken ct)
    {
        // Every poll also settles any lot whose timer has run out, so results never wait on a click.
        await settlement.SettleExpiredAsync(ct);
        var auction = await ActiveAuction(ct);
        var lot = await LotForAuction(auction.Id, ct);
        var activeSeatCutoff = DateTimeOffset.UtcNow - SeatTtl;
        var seats = await db.AuctionLiveSeats.AsNoTracking()
            .Where(x => x.AuctionId == auction.Id && x.LastSeenAtUtc >= activeSeatCutoff)
            .ToArrayAsync(ct);
        return Ok(ToState(auction, lot, seats));
    }

    [HttpPost("admin/lots")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> CreateLot(CreateLiveAuctionLotRequest request, CancellationToken ct)
    {
        if (request.StartingPrice <= 0) return BadRequest("Starting price must be greater than zero.");
        var auction = await ActiveAuction(ct);
        if (!await db.Players.AnyAsync(x => x.Id == request.PlayerId, ct)) return NotFound("Player not found.");
        if (await db.AuctionAssignments.AnyAsync(x => x.AuctionId == auction.Id && x.PlayerId == request.PlayerId, ct)) return Conflict("This player has already been assigned.");
        // Unsold and cancelled lots are finished, so those players may be put up again.
        if (await db.AuctionLots.AnyAsync(x => x.AuctionId == auction.Id && x.PlayerId == request.PlayerId && x.State != AuctionLotStates.Closed && x.State != AuctionLotStates.Cancelled, ct)) return Conflict("This player already has an active live-auction lot.");
        var lot = new AuctionLot { AuctionId = auction.Id, PlayerId = request.PlayerId, StartingPrice = request.StartingPrice };
        db.AuctionLots.Add(lot); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { }, await ToLotResponse(lot.Id, ct));
    }

    [HttpPost("admin/lots/{lotId:guid}/open")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> Open(Guid lotId, OpenLiveAuctionLotRequest request, CancellationToken ct)
    {
        var duration = request.DurationSeconds ?? 30;
        if (duration is < 10 or > 300) return BadRequest("Duration must be between 10 and 300 seconds.");
        var lot = await db.AuctionLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State is AuctionLotStates.Closed or AuctionLotStates.Cancelled) return BadRequest("Closed or cancelled lots cannot be opened.");
        if (await db.AuctionLots.AnyAsync(x => x.AuctionId == lot.AuctionId && x.Id != lot.Id && x.State == AuctionLotStates.Open, ct)) return Conflict("Only one live lot can be open at a time.");
        lot.State = AuctionLotStates.Open; lot.EndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(duration); lot.PausedRemainingSeconds = null; lot.ClosedAtUtc = null;
        await db.SaveChangesAsync(ct); return Ok(await ToLotResponse(lot.Id, ct));
    }

    [HttpPost("admin/lots/{lotId:guid}/pause")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> Pause(Guid lotId, CancellationToken ct)
    {
        var lot = await db.AuctionLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State != AuctionLotStates.Open || lot.EndsAtUtc is null) return BadRequest("Only an open lot can be paused.");
        lot.PausedRemainingSeconds = Math.Max(0, (int)Math.Ceiling((lot.EndsAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
        lot.State = AuctionLotStates.Paused;
        await db.SaveChangesAsync(ct);
        return Ok(await ToLotResponse(lot.Id, ct));
    }

    [HttpPost("admin/lots/{lotId:guid}/resume")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> Resume(Guid lotId, CancellationToken ct)
    {
        var lot = await db.AuctionLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State != AuctionLotStates.Paused) return BadRequest("Only a paused lot can be resumed.");
        if (lot.PausedRemainingSeconds is not int remainingSeconds || remainingSeconds <= 0)
            return BadRequest("This paused lot has expired and cannot be resumed.");
        lot.State = AuctionLotStates.Open;
        lot.EndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(remainingSeconds);
        lot.PausedRemainingSeconds = null;
        await db.SaveChangesAsync(ct);
        return Ok(await ToLotResponse(lot.Id, ct));
    }

    [HttpPost("lots/{lotId:guid}/bids")]
    public async Task<ActionResult<LiveAuctionLotResponse>> PlaceBid(Guid lotId, PlaceLiveAuctionBidRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var lot = await db.AuctionLots.Include(x => x.Bids).SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State != AuctionLotStates.Open || lot.EndsAtUtc is null || lot.EndsAtUtc <= DateTimeOffset.UtcNow) return Conflict("This lot is no longer accepting bids.");
        var team = await db.AuctionTeams.Include(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == request.AuctionTeamId && x.AuctionId == lot.AuctionId && x.Status == AuctionTeamStatuses.Approved, ct);
        if (team is null) return NotFound("Approved auction team not found.");
        if (team.LiveBidderUserId != userId) return Forbid();
        // The leading team cannot outbid itself; another team must bid first.
        if (lot.HighestBidAuctionTeamId == team.Id) return Conflict("You already hold the highest bid. Wait for another team to bid before bidding again.");
        var activeSeatCutoff = DateTimeOffset.UtcNow - SeatTtl;
        var hasLiveBidderSeat = await db.AuctionLiveSeats.AnyAsync(x =>
            x.AuctionId == lot.AuctionId &&
            x.UserId == userId &&
            x.AuctionTeamId == team.Id &&
            x.SeatKind == AuctionSeatKinds.Bidder &&
            x.LastSeenAtUtc >= activeSeatCutoff, ct);
        if (!hasLiveBidderSeat) return Conflict("Join a live bidding seat before placing a bid.");
        // The first accepted offer establishes the administrator-selected starting price.
        // Every later offer follows the agreed increment ladder.
        var expected = lot.CurrentBidAmount is null
            ? lot.StartingPrice
            : NextBid(lot.CurrentBidAmount.Value);
        if (request.Amount != expected) return BadRequest($"The next valid bid is {expected:0.##}.");
        if (team.StartingBalance - team.Assignments.Sum(x => x.SoldPrice) < request.Amount) return BadRequest("This team does not have sufficient remaining balance.");

        var now = DateTimeOffset.UtcNow;
        if (lot.EndsAtUtc.Value - now <= TimeSpan.FromSeconds(5)) { lot.EndsAtUtc = lot.EndsAtUtc.Value.AddSeconds(10); lot.ExtensionCount++; }
        lot.CurrentBidAmount = request.Amount; lot.HighestBidAuctionTeamId = team.Id;
        db.AuctionBids.Add(new AuctionBid { AuctionLotId = lot.Id, AuctionTeamId = team.Id, BidderUserId = userId, Amount = request.Amount });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Another bid was accepted first. Refresh the lot and try again."); }
        return Ok(await ToLotResponse(lot.Id, ct));
    }

    [HttpPost("admin/lots/{lotId:guid}/close")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> Close(Guid lotId, CancellationToken ct)
    {
        var lot = await db.AuctionLots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State is AuctionLotStates.Closed or AuctionLotStates.Cancelled) return BadRequest("This lot is already finalised.");
        // The shared settlement picks the winner, falling back to the next bidder who can still pay.
        await settlement.SettleAsync(lotId, requireExpired: false, ct);
        var settled = await ToLotResponse(lotId, ct);
        if (settled.State is not (AuctionLotStates.Closed or AuctionLotStates.Cancelled)) return Conflict("The lot changed while closing. Refresh and try again.");
        return Ok(settled);
    }

    [HttpPost("admin/lots/{lotId:guid}/cancel")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionLotResponse>> Cancel(Guid lotId, CancellationToken ct)
    {
        var lot = await db.AuctionLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return NotFound();
        if (lot.State == AuctionLotStates.Closed) return BadRequest("A closed lot cannot be cancelled.");
        lot.State = AuctionLotStates.Cancelled; lot.PausedRemainingSeconds = null; lot.ClosedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Ok(await ToLotResponse(lot.Id, ct));
    }

    /// <summary>Approved teams and the user currently authorised to bid for each team.</summary>
    [HttpGet("teams")]
    public async Task<ActionResult<LiveAuctionTeamResponse[]>> GetTeams(CancellationToken ct)
    {
        var auction = await ActiveAuction(ct);
        var teams = await TeamQuery(auction.Id).ToArrayAsync(ct);
        return Ok(teams.Select(ToTeamResponse).ToArray());
    }

    /// <summary>
    /// A team has one live bidder at a time. Administrators select that person
    /// from the team's representative and registered members before a live lot opens.
    /// </summary>
    [HttpPut("admin/teams/{teamId:guid}/live-bidder")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<LiveAuctionTeamResponse>> SetLiveBidder(Guid teamId, SetLiveAuctionBidderRequest request, CancellationToken ct)
    {
        var auction = await ActiveAuction(ct);
        var team = await db.AuctionTeams.Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == teamId && x.AuctionId == auction.Id && x.Status == AuctionTeamStatuses.Approved, ct);
        if (team is null) return NotFound("Approved auction team not found.");

        if (request.UserId is Guid userId)
        {
            var isTeamMember = team.RepresentativeUserId == userId || team.Members.Any(x => x.UserId == userId);
            if (!isTeamMember) return BadRequest("The selected user is not a member of this team.");

            var selectedElsewhere = await db.AuctionTeams.AnyAsync(x => x.AuctionId == auction.Id && x.Id != team.Id && x.Status == AuctionTeamStatuses.Approved && x.LiveBidderUserId == userId, ct);
            if (selectedElsewhere) return Conflict("This user is already the live bidder for another team.");
        }

        team.LiveBidderUserId = request.UserId;
        await db.SaveChangesAsync(ct);

        var updated = await TeamQuery(auction.Id).SingleAsync(x => x.Id == team.Id, ct);
        return Ok(ToTeamResponse(updated));
    }

    [HttpPost("seats/join")]
    public async Task<ActionResult<LiveAuctionSeatResponse>> Join(JoinLiveAuctionSeatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionId) || request.ConnectionId.Trim().Length > 128) return BadRequest("A valid connection id is required.");
        await SeatGate.WaitAsync(ct);
        try
        {
            var auction = await ActiveAuction(ct); await RemoveStaleSeats(auction.Id, ct); var userId = CurrentUserId();
            var kind = User.IsInRole(SystemRoles.Admin) ? AuctionSeatKinds.Admin : request.AuctionTeamId is Guid ? AuctionSeatKinds.Bidder : AuctionSeatKinds.Viewer;
            if (kind == AuctionSeatKinds.Bidder && !await db.AuctionTeams.AnyAsync(x => x.Id == request.AuctionTeamId && x.AuctionId == auction.Id && x.Status == AuctionTeamStatuses.Approved && x.LiveBidderUserId == userId, ct)) return Forbid();
            // A reconnect replaces the same user's existing seat rather than consuming
            // another one of the deliberately small live-room capacity slots.
            var seat = await db.AuctionLiveSeats.OrderByDescending(x => x.LastSeenAtUtc).FirstOrDefaultAsync(x => x.AuctionId == auction.Id && x.UserId == userId, ct);
        var occupiedQuery = db.AuctionLiveSeats.Where(x => x.AuctionId == auction.Id);
        if (seat is not null)
        {
            var seatId = seat.Id;
            occupiedQuery = occupiedQuery.Where(x => x.Id != seatId);
        }

        var occupied = await occupiedQuery.ToListAsync(ct);
            if (occupied.Count >= MaxConnections || occupied.Count(x => x.SeatKind == kind) >= CapacityFor(kind)) return Conflict(RoomFullMessage(kind));
            if (seat is null) { seat = new AuctionLiveSeat { AuctionId = auction.Id, UserId = userId, ConnectionId = request.ConnectionId.Trim() }; db.AuctionLiveSeats.Add(seat); }
            seat.ConnectionId = request.ConnectionId.Trim(); seat.AuctionTeamId = kind == AuctionSeatKinds.Bidder ? request.AuctionTeamId : null; seat.SeatKind = kind; seat.LastSeenAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct); return Ok(ToSeatResponse(seat));
        }
        finally
        {
            SeatGate.Release();
        }
    }

    [HttpPost("seats/heartbeat")]
    public async Task<IActionResult> Heartbeat(HeartbeatLiveAuctionSeatRequest request, CancellationToken ct)
    {
        var auction = await ActiveAuction(ct); var seat = await db.AuctionLiveSeats.OrderByDescending(x => x.LastSeenAtUtc).FirstOrDefaultAsync(x => x.AuctionId == auction.Id && x.ConnectionId == request.ConnectionId && x.UserId == CurrentUserId(), ct);
        if (seat is null) return NotFound(); seat.LastSeenAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpDelete("seats/{connectionId}")]
    public async Task<IActionResult> Leave(string connectionId, CancellationToken ct)
    {
        var auction = await ActiveAuction(ct); var seat = await db.AuctionLiveSeats.OrderByDescending(x => x.LastSeenAtUtc).FirstOrDefaultAsync(x => x.AuctionId == auction.Id && x.ConnectionId == connectionId && x.UserId == CurrentUserId(), ct);
        if (seat is null) return NoContent(); db.AuctionLiveSeats.Remove(seat); await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<Auction> ActiveAuction(CancellationToken ct)
    {
        var auction = await db.Auctions.SingleOrDefaultAsync(x => x.IsActive, ct); if (auction is not null) return auction;
        auction = new Auction { Name = "Football Mayhem Live Auction", IsActive = true }; db.Auctions.Add(auction); await db.SaveChangesAsync(ct); return auction;
    }
    private IQueryable<AuctionTeam> TeamQuery(Guid auctionId) => db.AuctionTeams.AsNoTracking()
        .Where(x => x.AuctionId == auctionId && x.Status == AuctionTeamStatuses.Approved)
        .Include(x => x.RepresentativeUser)
        .Include(x => x.LiveBidderUser)
        .Include(x => x.Members).ThenInclude(x => x.User)
        .OrderBy(x => x.TeamName);
    private async Task<AuctionLot?> LotForAuction(Guid auctionId, CancellationToken ct) => await db.AuctionLots.AsNoTracking().Where(x => x.AuctionId == auctionId).Include(x => x.Player).ThenInclude(x => x.Nationality).Include(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Player).ThenInclude(x => x.Images).Include(x => x.HighestBidAuctionTeam).Include(x => x.Bids.OrderByDescending(b => b.CreatedAtUtc).Take(20)).ThenInclude(x => x.AuctionTeam).OrderBy(x => x.State == AuctionLotStates.Open ? 0 : x.State == AuctionLotStates.Paused ? 1 : x.State == AuctionLotStates.Draft ? 2 : 3).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
    private async Task<LiveAuctionLotResponse> ToLotResponse(Guid lotId, CancellationToken ct) => ToLotResponse(await db.AuctionLots.AsNoTracking().Where(x => x.Id == lotId).Include(x => x.Player).ThenInclude(x => x.Nationality).Include(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Player).ThenInclude(x => x.Images).Include(x => x.HighestBidAuctionTeam).Include(x => x.Bids.OrderByDescending(b => b.CreatedAtUtc).Take(20)).ThenInclude(x => x.AuctionTeam).SingleAsync(ct));
    private static LiveAuctionStateResponse ToState(Auction auction, AuctionLot? lot, AuctionLiveSeat[] seats) => new(auction.Id, lot is null ? null : ToLotResponse(lot), new LiveAuctionCapacityResponse(MaxBidders, MaxViewers, MaxAdmins, MaxConnections, seats.Count(x => x.SeatKind == AuctionSeatKinds.Bidder), seats.Count(x => x.SeatKind == AuctionSeatKinds.Viewer), seats.Count(x => x.SeatKind == AuctionSeatKinds.Admin), seats.Length), DateTimeOffset.UtcNow);
    private static LiveAuctionLotResponse ToLotResponse(AuctionLot lot) => new(lot.Id, ToPlayer(lot.Player), lot.StartingPrice, lot.CurrentBidAmount, lot.State, lot.EndsAtUtc, lot.PausedRemainingSeconds, lot.ClosedAtUtc, lot.ExtensionCount, lot.HighestBidAuctionTeamId, lot.HighestBidAuctionTeam?.TeamName, lot.Bids.OrderByDescending(x => x.CreatedAtUtc).Select(x => new LiveAuctionBidResponse(x.Id, x.AuctionTeamId, x.AuctionTeam.TeamName, x.Amount, x.CreatedAtUtc)).ToArray());
    private static LiveAuctionPlayerResponse ToPlayer(Player player) => new(player.Id, player.FullName, player.Nationality.Name, player.PlayerPositions.OrderByDescending(x => x.IsPrimary).Select(x => x.Position.Name).ToArray(), player.PlayerPositions.OrderByDescending(x => x.IsPrimary).Select(x => x.Position.Code).FirstOrDefault() ?? "", player.OverallRank, player.Images.Where(x => x.IsPrimary).Select(x => "/player-images/" + x.BlobPath.Replace("\\", "/")).FirstOrDefault());
    private static LiveAuctionSeatResponse ToSeatResponse(AuctionLiveSeat seat) => new(seat.Id, seat.SeatKind, seat.AuctionTeamId, seat.LastSeenAtUtc);
    private static LiveAuctionTeamResponse ToTeamResponse(AuctionTeam team)
    {
        var members = team.Members
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new LiveAuctionTeamMemberResponse(x.UserId, x.User.DisplayName, x.User.Email))
            .ToList();
        if (members.All(x => x.Id != team.RepresentativeUserId))
            members.Insert(0, new LiveAuctionTeamMemberResponse(team.RepresentativeUserId, team.RepresentativeUser.DisplayName, team.RepresentativeUser.Email));

        return new LiveAuctionTeamResponse(team.Id, team.TeamName, team.Icon, team.RepresentativeUserId,
            team.RepresentativeUser.DisplayName, team.LiveBidderUserId, team.LiveBidderUser?.DisplayName, members.ToArray());
    }
    private async Task RemoveStaleSeats(Guid auctionId, CancellationToken ct)
    {
        var staleSeatCutoff = DateTimeOffset.UtcNow - SeatTtl;
        var stale = await db.AuctionLiveSeats
            .Where(x => x.AuctionId == auctionId && x.LastSeenAtUtc < staleSeatCutoff)
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        db.AuctionLiveSeats.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
    }
    private static decimal NextBid(decimal current) => current < 50 ? current + 5 : current < 100 ? current + 10 : current < 200 ? current + 15 : current < 300 ? current + 20 : current < 500 ? current + 30 : current + 50;
    private static int CapacityFor(string kind) => kind == AuctionSeatKinds.Bidder ? MaxBidders : kind == AuctionSeatKinds.Admin ? MaxAdmins : MaxViewers;
    private static string RoomFullMessage(string kind) => kind == AuctionSeatKinds.Bidder ? "All 12 live bidding seats are occupied. Please try again when a representative leaves." : kind == AuctionSeatKinds.Admin ? "All 3 admin live seats are occupied. Please try again when an admin leaves." : "All 10 viewer seats are occupied. Please try again when a viewer leaves.";
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record CreateLiveAuctionLotRequest(Guid PlayerId, decimal StartingPrice);
public sealed record OpenLiveAuctionLotRequest(int? DurationSeconds);
public sealed record PlaceLiveAuctionBidRequest(Guid AuctionTeamId, decimal Amount);
public sealed record SetLiveAuctionBidderRequest(Guid? UserId);
public sealed record JoinLiveAuctionSeatRequest(string ConnectionId, Guid? AuctionTeamId);
public sealed record HeartbeatLiveAuctionSeatRequest(string ConnectionId);
public sealed record LiveAuctionPlayerResponse(Guid Id, string FullName, string Nationality, string[] Positions, string PrimaryPositionCode, int OverallRank, string? PrimaryImageUrl);
public sealed record LiveAuctionBidResponse(Guid Id, Guid AuctionTeamId, string TeamName, decimal Amount, DateTimeOffset PlacedAtUtc);
public sealed record LiveAuctionLotResponse(Guid Id, LiveAuctionPlayerResponse Player, decimal StartingPrice, decimal? CurrentBidAmount, string State, DateTimeOffset? EndsAtUtc, int? PausedRemainingSeconds, DateTimeOffset? ClosedAtUtc, int ExtensionCount, Guid? HighestBidAuctionTeamId, string? HighestBidTeamName, LiveAuctionBidResponse[] RecentBids);
public sealed record LiveAuctionSeatResponse(Guid Id, string SeatKind, Guid? AuctionTeamId, DateTimeOffset LastSeenAtUtc);
public sealed record LiveAuctionCapacityResponse(int MaxBidders, int MaxViewers, int MaxAdmins, int MaxConnections, int ActiveBidders, int ActiveViewers, int ActiveAdmins, int ActiveConnections);
public sealed record LiveAuctionTeamMemberResponse(Guid Id, string DisplayName, string Email);
public sealed record LiveAuctionTeamResponse(Guid Id, string TeamName, string? Icon, Guid RepresentativeUserId, string RepresentativeName, Guid? LiveBidderUserId, string? LiveBidderName, LiveAuctionTeamMemberResponse[] Members);
public sealed record LiveAuctionStateResponse(Guid AuctionId, LiveAuctionLotResponse? CurrentLot, LiveAuctionCapacityResponse Capacity, DateTimeOffset ServerNowUtc);
