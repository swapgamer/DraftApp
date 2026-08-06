using System.Security.Claims;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.API.Controllers;

[ApiController, Authorize, Route("api/v1/favorites")]
public sealed class FavoritesController(DraftDatastoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FavoritePlayerResponse>>> Get(CancellationToken ct)
    {
        var userId = UserId();
        var favorites = await db.Favorites.AsNoTracking().Where(x => x.UserId == userId && !x.Player.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new FavoritePlayerResponse(x.PlayerId, x.Player.FullName, x.Player.OverallRank, x.Player.Nationality.Name, x.Player.PlayingEra.Name, x.Player.PlayerPositions.Select(p => p.Position.Name).ToArray()))
            .ToArrayAsync(ct);
        return Ok(favorites);
    }

    [HttpPost("{playerId:guid}")]
    public async Task<IActionResult> Add(Guid playerId, CancellationToken ct)
    {
        var userId = UserId();
        if (!await db.Players.AnyAsync(x => x.Id == playerId && !x.IsDeleted, ct)) return NotFound();
        if (await db.Favorites.AnyAsync(x => x.UserId == userId && x.PlayerId == playerId, ct)) return NoContent();
        db.Favorites.Add(new Favorite { UserId = userId, PlayerId = playerId });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{playerId:guid}")]
    public async Task<IActionResult> Remove(Guid playerId, CancellationToken ct)
    {
        var favorite = await db.Favorites.SingleOrDefaultAsync(x => x.UserId == UserId() && x.PlayerId == playerId, ct);
        if (favorite is null) return NotFound();
        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record FavoritePlayerResponse(Guid PlayerId, string FullName, int OverallRank, string Nationality, string PlayingEra, string[] Positions);
