using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Paolo Maldini", "Maldini", "https://www.transfermarkt.co.in/schnellsuche/ergebnis/schnellsuche?query=maldini"),
    new Target("Giacinto Facchetti", "Giacinto Facchetti", "https://www.transfermarkt.co.in/giacinto-facchetti/profil/spieler/145193"),
    new Target("Roberto Carlos", "Roberto Carlos", "https://www.transfermarkt.co.in/roberto-carlos/profil/spieler/7518"),
    new Target("Ruud Krol", "Ruud Krol", "https://www.transfermarkt.co.in/ruud-krol/profil/spieler/135621"),
    new Target("Nilton Santos", "Nílton Reis dos Santos", "https://www.transfermarkt.co.in/nilton-santos/profil/spieler/137265"),
    new Target("Ashley Cole", "Ashley Cole", "https://www.transfermarkt.co.in/ashley-cole/profil/spieler/3182"),
    new Target("Paul Breitner", "Paul Breitner", "https://www.transfermarkt.co.in/paul-breitner/profil/spieler/13766"),
    new Target("Marcelo", "Marcelo", "http://transfermarkt.co.in/marcelo/profil/spieler/44501"),
    new Target("Karl-Heinz Schnellinger", "Schnellinger", "https://www.transfermarkt.co.in/karl-heinz-schnellinger/profil/spieler/72381"),
    new Target("Antonio Cabrini", "Antonio Cabrini", "https://www.transfermarkt.co.in/antonio-cabrini/profil/spieler/116689"),
    new Target("Silvio Marzolini", "Silvio Marzolini", "https://www.transfermarkt.co.in/silvio-marzolini/profil/spieler/237146"),
    new Target("Andreas Brehme", "Andreas Brehme", "https://www.transfermarkt.co.in/andreas-brehme/profil/spieler/16056")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var leftBack = await db.Positions.SingleAsync(position => position.Code == "LB");
var resolved = new List<(Target Target, Player Player)>();
var unresolved = new List<string>();

foreach (var target in targets)
{
    var matches = players.Where(player => Matches(player, target.Lookup)).ToList();
    if (matches.Count != 1) { unresolved.Add($"{target.Name} ({matches.Count} matches)"); continue; }
    resolved.Add((target, matches[0]));
}
if (unresolved.Count > 0)
{
    Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:");
    foreach (var item in unresolved) Console.Error.WriteLine($"- {item}");
    return 1;
}

var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet();
var existingLeftBacks = players.Where(player => player.PlayerPositions.Any(position => position.PositionId == leftBack.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existingLeftBacks.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(position => position.PositionId != leftBack.Id)).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Left-back catalogue review: {resolved.Count} approved players, {existingLeftBacks.Count} current entries.");
Console.WriteLine($"Will remove Left-Back from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Left-Back to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }

await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(item => item.PositionId == leftBack.Id));
foreach (var (target, player) in resolved)
{
    if (player.PlayerPositions.All(position => position.PositionId != leftBack.Id))
        db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = leftBack.Id, IsPrimary = false });
    player.TransfermarktUrl = target.TransfermarktUrl;
}
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Left-back catalogue and Transfermarkt links updated successfully.");
return 0;

static bool Matches(Player player, string lookup) =>
    string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) ||
    player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));

sealed record Target(string Name, string Lookup, string TransfermarktUrl);
