using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Franz Beckenbauer", "Beckenbauer", "https://www.transfermarkt.co.in/franz-beckenbauer/profil/spieler/72347"),
    new Target("Franco Baresi", "Baresi", "https://www.transfermarkt.co.in/franco-baresi/profil/spieler/42049"),
    new Target("Gaetano Scirea", "Gaetano Scirea", "https://www.transfermarkt.co.in/gaetano-scirea/profil/spieler/116740"),
    new Target("Daniel Passarella", "Passarella", "https://www.transfermarkt.co.in/daniel-passarella/profil/spieler/116735"),
    new Target("Matthias Sammer", "Matthias Sammer", "https://www.transfermarkt.co.in/matthias-sammer/profil/spieler/77010"),
    new Target("Sergio Ramos", "Sergio Ramos", "https://www.transfermarkt.co.in/sergio-ramos/profil/spieler/25557"),
    new Target("Armando Picchi", "Armando Picchi", "https://www.transfermarkt.co.in/armando-picchi/profil/spieler/245245"),
    new Target("Ronald Koeman", "Ronald Koeman", "https://www.transfermarkt.co.in/ronald-koeman/profil/spieler/7940"),
    new Target("José Santamaría", "Santamaría", "https://www.transfermarkt.co.in/jose-santamaria/profil/spieler/137457"),
    new Target("Laurent Blanc", "Laurent Robert Blanc", "https://www.transfermarkt.co.in/laurent-blanc/profil/spieler/3113"),
    new Target("Fernando Hierro", "Hierro", "https://www.transfermarkt.co.in/fernando-hierro/profil/spieler/7513"),
    new Target("Rio Ferdinand", "Rio Gavin Ferdinand", "https://www.transfermarkt.co.in/rio-ferdinand/profil/spieler/3235"),
    new Target("Klaus Augenthaler", "Klaus Augenthaler", "https://www.transfermarkt.co.in/klaus-augenthaler/profil/spieler/85968"),
    new Target("Elías Figueroa", "Elías Ricardo Figueroa", "https://www.transfermarkt.co.in/elias-figueroa/profil/spieler/38537"),
    new Target("Marius Trésor", "Marius Paul Trésor", "https://www.transfermarkt.co.in/marius-tresor/profil/spieler/128995")
};
var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var position = await db.Positions.SingleAsync(item => item.Code == "CBSW");
var resolved = new List<(Target Target, Player Player)>(); var unresolved = new List<string>();
foreach (var target in targets) { var matches = players.Where(player => Matches(player, target.Lookup)).ToList(); if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)"); else resolved.Add((target, matches[0])); }
if (unresolved.Count > 0) { Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:"); foreach (var item in unresolved) Console.Error.WriteLine($"- {item}"); return 1; }
var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet();
var existing = players.Where(player => player.PlayerPositions.Any(item => item.PositionId == position.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existing.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(value => value.PositionId != position.Id)).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Centre-Back (Sweeper) catalogue review: {resolved.Count} approved players, {existing.Count} current entries.");
Console.WriteLine($"Will remove Centre-Back (Sweeper) from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Centre-Back (Sweeper) to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }
await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(item => item.PositionId == position.Id));
foreach (var (target, player) in resolved) { if (player.PlayerPositions.All(item => item.PositionId != position.Id)) db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = position.Id, IsPrimary = false }); player.TransfermarktUrl = target.TransfermarktUrl; }
await db.SaveChangesAsync(); await transaction.CommitAsync(); Console.WriteLine("Centre-Back (Sweeper) catalogue and Transfermarkt links updated successfully."); return 0;
static bool Matches(Player player, string lookup) => string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) || player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));
sealed record Target(string Name, string Lookup, string TransfermarktUrl);
