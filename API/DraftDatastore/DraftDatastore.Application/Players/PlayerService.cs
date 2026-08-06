using System.Globalization;
using System.Text;
using DraftDatastore.Domain.Entities;

namespace DraftDatastore.Application.Players;

public sealed class PlayerService(IPlayerRepository repository) : IPlayerService
{
    public async Task<PagedResult<PlayerResponse>> SearchAsync(PlayerSearchRequest request, CancellationToken ct)
    {
        var players = await repository.SearchAsync(request, ct);
        return new(players.Items.Select(Map).ToArray(), players.PageNumber, players.PageSize, players.TotalCount);
    }

    public async Task<PlayerResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var player = await repository.GetAsync(id, false, ct);
        return player is null ? null : Map(player);
    }

    public async Task<PlayerResponse> CreateAsync(PlayerUpsertRequest request, CancellationToken ct)
    {
        var player = Build(new Player(), request);
        await repository.AddAsync(player, ct);
        await repository.SaveAsync(ct);
        return Map(player);
    }

    public async Task<PlayerResponse?> UpdateAsync(Guid id, PlayerUpsertRequest request, CancellationToken ct)
    {
        var player = await repository.GetAsync(id, false, ct);
        if (player is null) return null;
        Build(player, request);
        await repository.SaveAsync(ct);
        return Map(player);
    }

    public async Task<PlayerResponse?> PatchAsync(Guid id, PlayerPatchRequest request, CancellationToken ct)
    {
        var player = await repository.GetAsync(id, false, ct);
        if (player is null) return null;
        ApplyPatch(player, request);
        await repository.SaveAsync(ct);
        return Map(player);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var player = await repository.GetAsync(id, false, ct);
        if (player is null) return false;
        player.IsDeleted = true;
        player.DeletedAtUtc = DateTimeOffset.UtcNow;
        await repository.SaveAsync(ct);
        return true;
    }

    public async Task<bool> RestoreAsync(Guid id, CancellationToken ct)
    {
        var player = await repository.GetAsync(id, true, ct);
        if (player is null || !player.IsDeleted) return false;
        player.IsDeleted = false;
        player.DeletedAtUtc = null;
        await repository.SaveAsync(ct);
        return true;
    }

    private static Player Build(Player player, PlayerUpsertRequest request)
    {
        player.FullName = request.FullName.Trim();
        player.NationalityId = request.NationalityId;
        player.PlayingEraId = request.PlayingEraId;
        player.ShortDescription = request.ShortDescription.Trim();
        player.OverallRank = request.OverallRank;
        player.GoalCreditPoints = request.GoalCreditPoints;
        player.AssistCreditPoints = request.AssistCreditPoints;
        player.DefensiveCreditPoints = request.DefensiveCreditPoints;
        player.TransfermarktUrl = request.TransfermarktUrl;
        player.WikipediaUrl = request.WikipediaUrl;
        SyncAliases(player, request.Aliases);
        SyncPositions(player, request.PositionIds, request.PrimaryPositionId);
        return player;
    }

    private static void ApplyPatch(Player player, PlayerPatchRequest request)
    {
        if (request.FullName is not null) player.FullName = request.FullName.Trim();
        if (request.NationalityId is not null) player.NationalityId = request.NationalityId.Value;
        if (request.PlayingEraId is not null) player.PlayingEraId = request.PlayingEraId.Value;
        if (request.ShortDescription is not null) player.ShortDescription = request.ShortDescription.Trim();
        if (request.OverallRank is not null) player.OverallRank = request.OverallRank.Value;
        if (request.GoalCreditPoints is not null) player.GoalCreditPoints = request.GoalCreditPoints.Value;
        if (request.AssistCreditPoints is not null) player.AssistCreditPoints = request.AssistCreditPoints.Value;
        if (request.DefensiveCreditPoints is not null) player.DefensiveCreditPoints = request.DefensiveCreditPoints.Value;
        if (request.TransfermarktUrl is not null) player.TransfermarktUrl = string.IsNullOrWhiteSpace(request.TransfermarktUrl) ? null : request.TransfermarktUrl.Trim();
        if (request.WikipediaUrl is not null) player.WikipediaUrl = string.IsNullOrWhiteSpace(request.WikipediaUrl) ? null : request.WikipediaUrl.Trim();
        if (request.Aliases is not null) SyncAliases(player, request.Aliases);
        if (request.PositionIds is not null) SyncPositions(player, request.PositionIds, request.PrimaryPositionId ?? request.PositionIds.First());
        else if (request.PrimaryPositionId is not null)
            foreach (var position in player.PlayerPositions) position.IsPrimary = position.PositionId == request.PrimaryPositionId.Value;
    }

    private static void SyncAliases(Player player, IEnumerable<string> requestedAliases)
    {
        var aliases = requestedAliases
            .Select(alias => alias.Trim())
            .Where(alias => alias.Length > 0)
            .GroupBy(AliasKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var requestedByKey = aliases.ToDictionary(AliasKey, StringComparer.Ordinal);

        foreach (var existing in player.Aliases.Where(alias => !requestedByKey.ContainsKey(AliasKey(alias.Alias))).ToList())
            player.Aliases.Remove(existing);

        foreach (var alias in aliases)
        {
            var key = AliasKey(alias);
            var existing = player.Aliases.FirstOrDefault(current => AliasKey(current.Alias) == key);
            if (existing is not null)
            {
                // Update Romario/Romário and casing edits in place: SQL Server treats these as the same unique key.
                if (!string.Equals(existing.Alias, alias, StringComparison.Ordinal)) existing.Alias = alias;
            }
            else
            {
                // An empty key tells EF this is a new dependent of an already tracked player.
                // Otherwise the entity's client-generated GUID can be interpreted as an existing row to update.
                player.Aliases.Add(new PlayerAlias { Id = Guid.Empty, PlayerId = player.Id, Alias = alias });
            }
        }
    }

    private static string AliasKey(string value) => string.Concat(value.Trim().Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC)
        .ToUpperInvariant();

    private static void SyncPositions(Player player, IReadOnlyCollection<int> requestedPositions, int primaryPositionId)
    {
        var positions = requestedPositions.Distinct().ToHashSet();
        foreach (var position in player.PlayerPositions.Where(position => !positions.Contains(position.PositionId)).ToList())
            player.PlayerPositions.Remove(position);
        foreach (var positionId in positions)
        {
            var current = player.PlayerPositions.SingleOrDefault(position => position.PositionId == positionId);
            if (current is null) player.PlayerPositions.Add(new PlayerPosition { PositionId = positionId, IsPrimary = positionId == primaryPositionId });
            else current.IsPrimary = positionId == primaryPositionId;
        }
    }

    private static PlayerResponse Map(Player player) => new(
        player.Id, player.FullName, player.Nationality.Name, player.PlayingEra.Name,
        player.PlayerPositions.Select(position => position.Position.Name).ToArray(),
        player.Aliases.Select(alias => alias.Alias).ToArray(), player.ShortDescription, player.OverallRank,
        player.GoalCreditPoints, player.AssistCreditPoints, player.DefensiveCreditPoints,
        player.TransfermarktUrl, player.WikipediaUrl,
        player.Images.OrderByDescending(image => image.IsPrimary).Select(image => "/player-images/" + image.BlobPath.Replace("\\", "/")).FirstOrDefault(),
        player.CreatedAtUtc, player.UpdatedAtUtc);
}
