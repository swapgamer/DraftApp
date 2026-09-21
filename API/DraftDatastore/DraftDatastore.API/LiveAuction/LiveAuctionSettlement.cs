using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.API.LiveAuction;

/// <summary>
/// Settles live lots. The admin Close action, the expiry worker and state polling all
/// go through here so a lot is finalised the same way no matter who triggers it.
/// </summary>
public sealed class LiveAuctionSettlement(DraftDatastoreDbContext db)
{
    /// <summary>Settles every open lot whose timer has run out. Returns how many were settled.</summary>
    public async Task<int> SettleExpiredAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var lotIds = await db.AuctionLots.AsNoTracking()
            .Where(x => x.State == AuctionLotStates.Open && x.EndsAtUtc != null && x.EndsAtUtc <= now)
            .Select(x => x.Id)
            .ToArrayAsync(ct);

        var settled = 0;
        foreach (var lotId in lotIds)
        {
            if (await SettleAsync(lotId, requireExpired: true, ct)) settled++;
        }

        return settled;
    }

    /// <summary>
    /// Closes a lot. The highest bidder wins if they can still afford it; otherwise the next
    /// highest bidder who can afford their own bid wins at that bid. With no eligible bidder
    /// the lot closes unsold and the player returns to the pool.
    /// Returns false when nothing was changed (already finalised, not yet expired, or a bid
    /// or another settler got there first).
    /// </summary>
    public async Task<bool> SettleAsync(Guid lotId, bool requireExpired, CancellationToken ct)
    {
        var lot = await db.AuctionLots.Include(x => x.Bids).SingleOrDefaultAsync(x => x.Id == lotId, ct);
        if (lot is null || lot.State is AuctionLotStates.Closed or AuctionLotStates.Cancelled) return false;

        var now = DateTimeOffset.UtcNow;
        if (requireExpired && (lot.State != AuctionLotStates.Open || lot.EndsAtUtc is null || lot.EndsAtUtc > now)) return false;

        lot.PausedRemainingSeconds = null;
        lot.ClosedAtUtc = now;

        if (await db.AuctionAssignments.AnyAsync(x => x.AuctionId == lot.AuctionId && x.PlayerId == lot.PlayerId, ct))
        {
            // The player was assigned by hand while the lot was live, so nothing can be sold here.
            lot.State = AuctionLotStates.Cancelled;
            lot.HighestBidAuctionTeamId = null;
            lot.CurrentBidAmount = null;
        }
        else
        {
            lot.State = AuctionLotStates.Closed;
            var sold = false;
            var candidates = lot.Bids
                .GroupBy(x => x.AuctionTeamId)
                .Select(x => new { TeamId = x.Key, Amount = x.Max(b => b.Amount) })
                .OrderByDescending(x => x.Amount);

            foreach (var candidate in candidates)
            {
                var team = await db.AuctionTeams.AsNoTracking()
                    .Where(x => x.Id == candidate.TeamId && x.Status == AuctionTeamStatuses.Approved)
                    .Select(x => new { x.StartingBalance })
                    .SingleOrDefaultAsync(ct);
                if (team is null) continue;

                var spent = await db.AuctionAssignments
                    .Where(x => x.AuctionTeamId == candidate.TeamId)
                    .SumAsync(x => (decimal?)x.SoldPrice, ct) ?? 0m;
                if (team.StartingBalance - spent < candidate.Amount) continue;

                db.AuctionAssignments.Add(new AuctionAssignment { AuctionId = lot.AuctionId, AuctionTeamId = candidate.TeamId, PlayerId = lot.PlayerId, SoldPrice = candidate.Amount });
                lot.HighestBidAuctionTeamId = candidate.TeamId;
                lot.CurrentBidAmount = candidate.Amount;
                sold = true;
                break;
            }

            if (!sold)
            {
                lot.HighestBidAuctionTeamId = null;
                lot.CurrentBidAmount = null;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // A late bid changed the lot, another settler finished first, or the player was
            // assigned in between. The next pass sees the fresh state and decides again.
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
