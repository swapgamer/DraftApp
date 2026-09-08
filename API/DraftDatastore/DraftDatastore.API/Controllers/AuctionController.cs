using System.Security.Claims;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.API.Controllers;

/// <summary>One live auction. The player catalogue remains untouched; assignments are auction-specific records.</summary>
[ApiController, Authorize, Route("api/v1/auction")]
public sealed class AuctionController(DraftDatastoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuctionStateResponse>> Get(CancellationToken ct)
    {
        var auction = await ActiveAuction(ct);
        var teams = await Teams(auction.Id, approvedOnly: true, ct);
        var assigned = teams.SelectMany(x => x.Assignments).Select(x => x.PlayerId).ToHashSet();
        var pool = await db.Players.AsNoTracking().Include(x => x.Nationality).Include(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Images)
            .Where(x => !assigned.Contains(x.Id)).OrderBy(x => x.OverallRank).Select(PlayerProjection()).ToArrayAsync(ct);
        return Ok(new AuctionStateResponse(auction.Id, auction.Name, teams.Select(ToResponse).ToArray(), pool));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<AuctionUserResponse>>> Users([FromQuery] string? search, CancellationToken ct)
    {
        var term = search?.Trim();
        var query = db.Users.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.DisplayName.Contains(term) || x.Email.Contains(term));
        return Ok(await query.OrderBy(x => x.DisplayName).Take(20).Select(x => new AuctionUserResponse(x.Id, x.DisplayName, x.Email)).ToArrayAsync(ct));
    }

    [HttpPost("team-requests")]
    public async Task<ActionResult<AuctionTeamResponse>> RequestTeam(CreateAuctionTeamRequest request, CancellationToken ct)
    {
        var error = await ValidateTeamRequest(request, ct);
        if (error is not null) return BadRequest(error);
        var auction = await ActiveAuction(ct);
        if (await db.AuctionTeams.AnyAsync(x => x.AuctionId == auction.Id && x.TeamName == request.TeamName.Trim(), ct)) return Conflict("That team name is already in use.");
        var representative = CurrentUserId();
        var memberIds = (request.MemberUserIds ?? []).Append(representative).Distinct().ToArray();
        var team = new AuctionTeam { AuctionId = auction.Id, RepresentativeUserId = representative, TeamName = request.TeamName.Trim(), Icon = Clean(request.Icon), Status = AuctionTeamStatuses.Pending };
        team.Members = memberIds.Select(id => new AuctionTeamMember { UserId = id }).ToList();
        db.AuctionTeams.Add(team); await db.SaveChangesAsync(ct);
        var created = await TeamById(team.Id, ct);
        return CreatedAtAction(nameof(Get), new { }, ToResponse(created));
    }

    [HttpGet("my-team-requests")]
    public async Task<ActionResult<IReadOnlyCollection<AuctionTeamResponse>>> MyRequests(CancellationToken ct)
    {
        var auction = await ActiveAuction(ct); var userId = CurrentUserId();
        var teams = await db.AuctionTeams.AsNoTracking().Where(x => x.AuctionId == auction.Id && x.Members.Any(m => m.UserId == userId))
            .Include(x => x.RepresentativeUser).Include(x => x.Members).ThenInclude(x => x.User).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Nationality)
            .Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Images).ToListAsync(ct);
        return Ok(teams.Select(ToResponse));
    }

    [HttpGet("admin/requests")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<IReadOnlyCollection<AuctionTeamResponse>>> PendingRequests(CancellationToken ct)
    {
        var auction = await ActiveAuction(ct); return Ok((await Teams(auction.Id, approvedOnly: false, ct)).Where(x => x.Status == AuctionTeamStatuses.Pending).Select(ToResponse));
    }

    [HttpPost("admin/teams/{teamId:guid}/approve")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<AuctionTeamResponse>> Approve(Guid teamId, ApproveAuctionTeamRequest request, CancellationToken ct)
    {
        if (request.StartingBalance < 0) return BadRequest("Starting balance cannot be negative.");
        var team = await db.AuctionTeams.SingleOrDefaultAsync(x => x.Id == teamId, ct);
        if (team is null) return NotFound(); if (team.Status != AuctionTeamStatuses.Pending) return BadRequest("Only pending requests can be approved.");
        team.StartingBalance = request.StartingBalance; team.Status = AuctionTeamStatuses.Approved; await db.SaveChangesAsync(ct);
        return Ok(ToResponse(await TeamById(teamId, ct)));
    }

    [HttpPost("admin/teams/{teamId:guid}/reject")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<IActionResult> Reject(Guid teamId, CancellationToken ct)
    {
        var team = await db.AuctionTeams.SingleOrDefaultAsync(x => x.Id == teamId, ct); if (team is null) return NotFound(); team.Status = AuctionTeamStatuses.Rejected; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPut("admin/teams/{teamId:guid}/balance")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<AuctionTeamResponse>> SetBalance(Guid teamId, SetAuctionTeamBalanceRequest request, CancellationToken ct)
    {
        if (request.StartingBalance < 0) return BadRequest("Starting balance cannot be negative.");
        var team = await db.AuctionTeams.Include(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == teamId, ct); if (team is null) return NotFound();
        if (team.Assignments.Sum(x => x.SoldPrice) > request.StartingBalance) return BadRequest("Starting balance cannot be below the amount already spent.");
        team.StartingBalance = request.StartingBalance; await db.SaveChangesAsync(ct); return Ok(ToResponse(await TeamById(teamId, ct)));
    }

    [HttpPost("admin/assignments")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<AuctionAssignmentResponse>> Assign(AssignAuctionPlayerRequest request, CancellationToken ct)
    {
        if (request.SoldPrice <= 0) return BadRequest("Sold price must be greater than zero.");
        var auction = await ActiveAuction(ct);
        var team = await db.AuctionTeams.Include(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == request.TeamId && x.AuctionId == auction.Id && x.Status == AuctionTeamStatuses.Approved, ct);
        if (team is null) return NotFound("Approved auction team not found.");
        if (!await db.Players.AnyAsync(x => x.Id == request.PlayerId, ct)) return NotFound("Player not found.");
        if (await db.AuctionAssignments.AnyAsync(x => x.AuctionId == auction.Id && x.PlayerId == request.PlayerId, ct)) return Conflict("This player has already been assigned.");
        var remaining = team.StartingBalance - team.Assignments.Sum(x => x.SoldPrice);
        if (remaining < request.SoldPrice) return BadRequest("This team does not have sufficient remaining balance.");
        var assignment = new AuctionAssignment { AuctionId = auction.Id, AuctionTeamId = team.Id, PlayerId = request.PlayerId, SoldPrice = request.SoldPrice };
        // SaveChanges is already atomic for this one insert. Do not open a manual transaction here:
        // Azure's configured retry execution strategy cannot run user-created transactions.
        db.AuctionAssignments.Add(assignment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("This player has already been assigned.");
        }
        var saved = await db.AuctionAssignments.AsNoTracking().Include(x => x.Player).ThenInclude(x => x.Nationality).Include(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Player).ThenInclude(x => x.Images).SingleAsync(x => x.Id == assignment.Id, ct);
        return Ok(ToResponse(saved));
    }

    [HttpDelete("admin/assignments/{assignmentId:guid}")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<IActionResult> RemoveAssignment(Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.AuctionAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct); if (assignment is null) return NotFound(); db.AuctionAssignments.Remove(assignment); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpDelete("admin/reset")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        var auction = await ActiveAuction(ct); db.AuctionTeams.RemoveRange(db.AuctionTeams.Where(x => x.AuctionId == auction.Id)); await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<Auction> ActiveAuction(CancellationToken ct)
    {
        var auction = await db.Auctions.SingleOrDefaultAsync(x => x.IsActive, ct);
        if (auction is not null) return auction;
        auction = new Auction { Name = "Football Mayhem Live Auction", IsActive = true }; db.Auctions.Add(auction); await db.SaveChangesAsync(ct); return auction;
    }
    private async Task<List<AuctionTeam>> Teams(Guid auctionId, bool approvedOnly, CancellationToken ct)
    {
        var query = db.AuctionTeams.AsNoTracking().Where(x => x.AuctionId == auctionId);
        if (approvedOnly) query = query.Where(x => x.Status == AuctionTeamStatuses.Approved);
        return await query.Include(x => x.RepresentativeUser).Include(x => x.Members).ThenInclude(x => x.User).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Nationality)
            .Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Images).OrderBy(x => x.TeamName).ToListAsync(ct);
    }
    private async Task<AuctionTeam> TeamById(Guid id, CancellationToken ct) => (await TeamsForIds([id], ct)).Single();
    private async Task<List<AuctionTeam>> TeamsForIds(Guid[] ids, CancellationToken ct) => await db.AuctionTeams.AsNoTracking().Where(x => ids.Contains(x.Id)).Include(x => x.RepresentativeUser).Include(x => x.Members).ThenInclude(x => x.User).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Nationality).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Assignments).ThenInclude(x => x.Player).ThenInclude(x => x.Images).ToListAsync(ct);
    private async Task<string?> ValidateTeamRequest(CreateAuctionTeamRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TeamName) || request.TeamName.Trim().Length > 120) return "Enter a team name of up to 120 characters.";
        var memberIds = request.MemberUserIds ?? [];
        if (memberIds.Count > 12) return "A team can have up to 12 members.";
        var ids = memberIds.Append(CurrentUserId()).Distinct().ToArray();
        return await db.Users.CountAsync(x => ids.Contains(x.Id) && x.IsActive, ct) == ids.Length ? null : "One or more nominated members are not active registered users.";
    }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static AuctionTeamResponse ToResponse(AuctionTeam team)
    {
        var assignments = team.Assignments.OrderBy(x => x.CreatedAtUtc).Select(ToResponse).ToArray(); var spent = assignments.Sum(x => x.SoldPrice);
        return new AuctionTeamResponse(team.Id, team.TeamName, team.Icon, team.Status, new AuctionUserResponse(team.RepresentativeUserId, team.RepresentativeUser.DisplayName, team.RepresentativeUser.Email), team.Members.Select(x => new AuctionUserResponse(x.UserId, x.User.DisplayName, x.User.Email)).ToArray(), team.StartingBalance, spent, team.StartingBalance - spent, assignments);
    }
    private static AuctionAssignmentResponse ToResponse(AuctionAssignment assignment) => new(assignment.Id, PlayerProjectionValue(assignment.Player), assignment.SoldPrice, assignment.CreatedAtUtc);
    private static AuctionPlayerResponse PlayerProjectionValue(Player player) => new(player.Id, player.FullName, player.Nationality.Name, player.PlayerPositions.OrderByDescending(x => x.IsPrimary).Select(x => x.Position.Name).ToArray(), player.PlayerPositions.OrderByDescending(x => x.IsPrimary).Select(x => x.Position.Code).FirstOrDefault() ?? "", player.OverallRank, player.Images.Where(x => x.IsPrimary).Select(x => "/player-images/" + x.BlobPath.Replace("\\", "/")).FirstOrDefault());
    private static System.Linq.Expressions.Expression<Func<Player, AuctionPlayerResponse>> PlayerProjection() => x => new AuctionPlayerResponse(x.Id, x.FullName, x.Nationality.Name, x.PlayerPositions.OrderByDescending(p => p.IsPrimary).Select(p => p.Position.Name).ToArray(), x.PlayerPositions.OrderByDescending(p => p.IsPrimary).Select(p => p.Position.Code).FirstOrDefault() ?? "", x.OverallRank, x.Images.Where(i => i.IsPrimary).Select(i => "/player-images/" + i.BlobPath.Replace("\\", "/")).FirstOrDefault());
}

public sealed record CreateAuctionTeamRequest(string TeamName, string? Icon, IReadOnlyCollection<Guid>? MemberUserIds);
public sealed record ApproveAuctionTeamRequest(decimal StartingBalance);
public sealed record SetAuctionTeamBalanceRequest(decimal StartingBalance);
public sealed record AssignAuctionPlayerRequest(Guid TeamId, Guid PlayerId, decimal SoldPrice);
public sealed record AuctionUserResponse(Guid Id, string DisplayName, string Email);
public sealed record AuctionPlayerResponse(Guid Id, string FullName, string Nationality, string[] Positions, string PrimaryPositionCode, int OverallRank, string? PrimaryImageUrl);
public sealed record AuctionAssignmentResponse(Guid Id, AuctionPlayerResponse Player, decimal SoldPrice, DateTimeOffset AssignedAtUtc);
public sealed record AuctionTeamResponse(Guid Id, string TeamName, string? Icon, string Status, AuctionUserResponse Representative, AuctionUserResponse[] Members, decimal StartingBalance, decimal TotalSpent, decimal RemainingBalance, AuctionAssignmentResponse[] Assignments);
public sealed record AuctionStateResponse(Guid AuctionId, string Name, AuctionTeamResponse[] Teams, AuctionPlayerResponse[] AvailablePlayers);
