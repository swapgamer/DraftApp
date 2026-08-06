using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DraftDatastore.API.Controllers;

[ApiController, Authorize, Route("api/v1/chemistry")]
public sealed class ChemistryController(DraftDatastoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChemistryPageResponse>> Browse([FromQuery] string? type, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        if (!TryNormalizeType(type, out var normalizedType)) return BadRequest("Type must be duo or trio.");
        pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize, 1, 48);
        var query = Combinations().Where(x => x.Type == normalizedType);
        return Ok(await Page(query, pageNumber, pageSize, ct));
    }

    [HttpGet("search")]
    public async Task<ActionResult<ChemistrySearchResponse>> Search([FromQuery] string? player, CancellationToken ct)
    {
        var term = player?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return BadRequest("Enter a player name or alias.");
        var matches = Combinations().Where(x => x.Players.Any(member => member.Player.FullName.Contains(term) || member.Player.Aliases.Any(alias => alias.Alias.Contains(term))));
        return Ok(new ChemistrySearchResponse(
            await matches.Where(x => x.Type == ChemistryTypes.Duo).OrderByDescending(x => x.CreatedAtUtc).Select(Project()).ToArrayAsync(ct),
            await matches.Where(x => x.Type == ChemistryTypes.Trio).OrderByDescending(x => x.CreatedAtUtc).Select(Project()).ToArrayAsync(ct)));
    }

    [HttpGet("admin")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<IReadOnlyList<ChemistryCombinationResponse>> AdminList(CancellationToken ct) => await Combinations().OrderBy(x => x.Type).ThenByDescending(x => x.CreatedAtUtc).Select(Project()).ToArrayAsync(ct);

    [HttpPost]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<ChemistryCombinationResponse>> Create(ChemistryCombinationRequest request, CancellationToken ct)
    {
        var error = await Validate(request, ct); if (error is not null) return BadRequest(error);
        var combination = new ChemistryCombination { Type = Normalize(request.Type), Title = CleanTitle(request.Title) };
        combination.Players = request.PlayerIds.Select((id, index) => new ChemistryCombinationPlayer { PlayerId = id, DisplayOrder = index + 1 }).ToList();
        db.ChemistryCombinations.Add(combination); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(AdminList), new { id = combination.Id }, await GetResponse(combination.Id, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<ActionResult<ChemistryCombinationResponse>> Update(Guid id, ChemistryCombinationRequest request, CancellationToken ct)
    {
        var error = await Validate(request, ct); if (error is not null) return BadRequest(error);
        var combination = await db.ChemistryCombinations.Include(x => x.Players).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (combination is null) return NotFound();
        combination.Type = Normalize(request.Type); combination.Title = CleanTitle(request.Title);
        db.ChemistryCombinationPlayers.RemoveRange(combination.Players);
        combination.Players = request.PlayerIds.Select((playerId, index) => new ChemistryCombinationPlayer { ChemistryCombinationId = id, PlayerId = playerId, DisplayOrder = index + 1 }).ToList();
        await db.SaveChangesAsync(ct); return Ok(await GetResponse(id, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = SystemRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var combination = await db.ChemistryCombinations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (combination is null) return NotFound();
        db.ChemistryCombinations.Remove(combination); await db.SaveChangesAsync(ct); return NoContent();
    }

    private IQueryable<ChemistryCombination> Combinations() => db.ChemistryCombinations.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player).ThenInclude(x => x.Nationality).Include(x => x.Players).ThenInclude(x => x.Player).ThenInclude(x => x.PlayingEra).Include(x => x.Players).ThenInclude(x => x.Player).ThenInclude(x => x.PlayerPositions).ThenInclude(x => x.Position).Include(x => x.Players).ThenInclude(x => x.Player).ThenInclude(x => x.Images).Include(x => x.Players).ThenInclude(x => x.Player).ThenInclude(x => x.Aliases);
    private static Expression<Func<ChemistryCombination, ChemistryCombinationResponse>> Project() => x => new ChemistryCombinationResponse(x.Id, x.Type, x.Title, x.Players.OrderBy(p => p.DisplayOrder).Select(p => new ChemistryPlayerResponse(p.Player.Id, p.Player.FullName, p.Player.Nationality.Name, p.Player.PlayingEra.Name, p.Player.PlayerPositions.OrderByDescending(position => position.IsPrimary).Select(position => position.Position.Name).ToArray(), p.Player.OverallRank, p.Player.GoalCreditPoints, p.Player.AssistCreditPoints, p.Player.DefensiveCreditPoints, p.Player.ShortDescription, p.Player.Images.Where(image => image.IsPrimary).Select(image => "/player-images/" + image.BlobPath.Replace("\\", "/")).FirstOrDefault())).ToArray());
    private static async Task<ChemistryPageResponse> Page(IQueryable<ChemistryCombination> query, int pageNumber, int pageSize, CancellationToken ct) { var total = await query.CountAsync(ct); var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(Project()).ToArrayAsync(ct); return new ChemistryPageResponse(items, pageNumber, pageSize, total); }
    private async Task<ChemistryCombinationResponse> GetResponse(Guid id, CancellationToken ct) => await Combinations().Where(x => x.Id == id).Select(Project()).SingleAsync(ct);
    private async Task<string?> Validate(ChemistryCombinationRequest request, CancellationToken ct) { if (!TryNormalizeType(request.Type, out var type)) return "Type must be duo or trio."; var expected = type == ChemistryTypes.Duo ? 2 : 3; if (request.PlayerIds is null || request.PlayerIds.Count != expected || request.PlayerIds.Distinct().Count() != expected) return $"A {type} must contain exactly {expected} different players."; var existing = await db.Players.CountAsync(x => request.PlayerIds.Contains(x.Id), ct); return existing == expected ? null : "One or more selected players could not be found."; }
    private static bool TryNormalizeType(string? type, out string normalized) { normalized = Normalize(type); return normalized is ChemistryTypes.Duo or ChemistryTypes.Trio; }
    private static string Normalize(string? type) => type?.Trim().ToLowerInvariant() switch { "duo" => ChemistryTypes.Duo, "trio" => ChemistryTypes.Trio, _ => string.Empty };
    private static string? CleanTitle(string? title) => string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

public static class ChemistryTypes { public const string Duo = "duo"; public const string Trio = "trio"; }
public sealed record ChemistryCombinationRequest(string Type, string? Title, IReadOnlyList<Guid> PlayerIds);
public sealed record ChemistryPlayerResponse(Guid Id, string FullName, string Nationality, string PlayingEra, string[] Positions, int OverallRank, decimal GoalCreditPoints, decimal AssistCreditPoints, decimal DefensiveCreditPoints, string ShortDescription, string? PrimaryImageUrl);
public sealed record ChemistryCombinationResponse(Guid Id, string Type, string? Title, ChemistryPlayerResponse[] Players);
public sealed record ChemistryPageResponse(ChemistryCombinationResponse[] Items, int PageNumber, int PageSize, int TotalCount);
public sealed record ChemistrySearchResponse(ChemistryCombinationResponse[] Duos, ChemistryCombinationResponse[] Trios);
