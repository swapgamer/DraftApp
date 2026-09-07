using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Lionel Messi", "Lionel Andrés Messi", 1, null, true),
    new Target("Garrincha", "The Little Bird", 2, "https://www.transfermarkt.co.in/mane-garrincha/profil/spieler/151263"),
    new Target("Jairzinho", "Jairzinho", 3, "https://www.transfermarkt.co.in/jairzinho/profil/spieler/145510"),
    new Target("George Best", "George Best", 3, "https://www.transfermarkt.co.in/george-best/profil/spieler/174986", true),
    new Target("Luis Figo", "Figo", 4, "https://www.transfermarkt.co.in/luis-figo/profil/spieler/3446"),
    new Target("Arjen Robben", "Arjen Robben", 5, "https://www.transfermarkt.co.in/arjen-robben/profil/spieler/4360"),
    new Target("David Beckham", "Becks", 5, "https://www.transfermarkt.co.in/david-beckham/profil/spieler/3139"),
    new Target("Raymond Kopa", "Little Napoleon", 6, "https://www.transfermarkt.co.in/raymond-kopa/profil/spieler/170730"),
    new Target("Stanley Matthews", "SSM", 6, "https://www.transfermarkt.co.in/sir-stanley-matthews/profil/spieler/212779"),
    new Target("Helmut Rahn", "Der Boss", 7, "https://www.transfermarkt.co.in/helmut-rahn/profil/spieler/89228"),
    new Target("Allan Simonsen", "Allan Rodenkam Simonsen", 7, "https://www.transfermarkt.co.in/allan-simonsen/profil/spieler/8026"),
    new Target("Grzegorz Lato", "Grzegorz", 8, "https://www.transfermarkt.co.in/grzegorz-lato/profil/spieler/132457"),
    new Target("Amancio", "El Brujo", 9, "https://www.transfermarkt.co.in/amancio/profil/spieler/135743"),
    new Target("Jimmy Johnstone", "Jinky", 10, "https://www.transfermarkt.co.in/jimmy-johnstone/profil/spieler/227368")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var rightWing = await db.Positions.SingleAsync(position => position.Code == "RW");
var resolved = new List<(Target Target, Player Player)>(); var unresolved = new List<string>();
foreach (var target in targets) { var matches = players.Where(player => Matches(player, target.Lookup)).ToList(); if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)"); else resolved.Add((target, matches[0])); }
if (unresolved.Count > 0) { Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:"); foreach (var target in unresolved) Console.Error.WriteLine($"- {target}"); return 1; }
var approvedIds = resolved.Select(item => item.Player.Id).ToHashSet(); var currentEntries = players.Where(player => player.PlayerPositions.Any(position => position.PositionId == rightWing.Id)).OrderBy(player => player.FullName).ToList(); var removals = currentEntries.Where(player => !approvedIds.Contains(player.Id)).ToList(); var additions = resolved.Where(item => item.Player.PlayerPositions.All(position => position.PositionId != rightWing.Id)).ToList(); var rankUpdates = resolved.Where(item => item.Player.PlayerPositions.SingleOrDefault(position => position.PositionId == rightWing.Id)?.OverallRank != item.Target.Rank).ToList(); var linkUpdates = resolved.Where(item => item.Target.TransfermarktUrl is not null && !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Right Wing catalogue review: {resolved.Count} approved players, {currentEntries.Count} current entries."); Console.WriteLine($"Will remove Right Wing from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}"); Console.WriteLine($"Will add Right Wing to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}"); Console.WriteLine($"Will set Right Wing-specific ranks for: {(rankUpdates.Count == 0 ? "none" : string.Join(", ", rankUpdates.Select(item => $"{item.Player.FullName} #{item.Target.Rank}")))}"); Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}"); Console.WriteLine("Exception entries retain all of their other position mappings.");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }
await using var transaction = await db.Database.BeginTransactionAsync(); foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(position => position.PositionId == rightWing.Id)); foreach (var (target, player) in resolved) { var mapping = player.PlayerPositions.SingleOrDefault(position => position.PositionId == rightWing.Id); if (mapping is null) { mapping = new PlayerPosition { PlayerId = player.Id, PositionId = rightWing.Id, IsPrimary = false }; db.PlayerPositions.Add(mapping); } mapping.OverallRank = target.Rank; if (target.TransfermarktUrl is not null) player.TransfermarktUrl = target.TransfermarktUrl; } await db.SaveChangesAsync(); await transaction.CommitAsync(); Console.WriteLine("Right Wing catalogue, ranks, and Transfermarkt links updated successfully."); return 0;
static bool Matches(Player player, string lookup) => string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) || player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));
sealed record Target(string Name, string Lookup, int Rank, string? TransfermarktUrl, bool IsException = false);
