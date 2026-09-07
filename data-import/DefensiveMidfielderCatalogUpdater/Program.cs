using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Frank Rijkaard", "Frank Rijkaard", "https://www.transfermarkt.co.in/frank-rijkaard/profil/spieler/70667"),
    new Target("Claude Makelele", "Makelele", "https://www.transfermarkt.co.in/claude-makelele/profil/spieler/4182"),
    new Target("Lothar Matthäus", "Lothar Herbert Matthäus", "https://www.transfermarkt.co.in/lothar-matthaus/profil/spieler/1527"),
    new Target("Sergio Busquets", "Sergio Busquets Burgos", "https://www.transfermarkt.co.in/sergio-busquets/profil/spieler/65230"),
    new Target("Marco Tardelli", "Marco Tardelli", "https://www.transfermarkt.co.in/marco-tardelli/profil/spieler/116744"),
    new Target("Roy Keane", "Roy Maurice Keane", "https://www.transfermarkt.co.in/roy-keane/profil/spieler/3396"),
    new Target("Fernando Redondo", "Fernando Carlos Redondo", "https://www.transfermarkt.co.in/fernando-redondo/profil/spieler/5811"),
    new Target("Patrick Vieira", "Patrick Vieira", "https://www.transfermarkt.co.in/patrick-vieira/profil/spieler/3183"),
    new Target("José Andrade", "José Leandro Andrade", "https://www.transfermarkt.co.in/jose-leandro-andrade/profil/spieler/229571"),
    new Target("Casemiro", "Carlos Henrique Casimiro", "https://www.transfermarkt.co.in/casemiro/profil/spieler/16306"),
    new Target("Gennaro Gattuso", "Gennaro Ivan Gattuso", "https://www.transfermarkt.co.in/gennaro-gattuso/profil/spieler/5813"),
    new Target("Graeme Souness", "Graeme James Souness", "https://www.transfermarkt.co.in/graeme-souness/profil/spieler/116155"),
    new Target("Pep Guardiola", "Josep Guardiola Sala", "https://www.transfermarkt.co.in/pep-guardiola/profil/spieler/5950"),
    new Target("Gilberto Silva", "Gilberto Aparecido da Silva", "https://www.transfermarkt.co.in/gilberto-silva/profil/spieler/3194"),
    new Target("Didier Deschamps", "Didier Claude Deschamps", "https://www.transfermarkt.co.in/didier-deschamps/profil/spieler/75553"),
    new Target("Luis Monti", "Luis Felipe Monti", "https://www.transfermarkt.co.in/luis-monti/profil/spieler/229641"),
    new Target("Xabi Alonso", "Xabier Alonso Olano", "https://www.transfermarkt.co.in/xabi-alonso/profil/spieler/7476")
};
var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var position = await db.Positions.SingleAsync(item => item.Code == "DM");
var resolved = new List<(Target Target, Player Player)>(); var unresolved = new List<string>();
foreach (var target in targets) { var matches = players.Where(player => Matches(player, target.Lookup)).ToList(); if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)"); else resolved.Add((target, matches[0])); }
if (unresolved.Count > 0) { Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:"); foreach (var item in unresolved) Console.Error.WriteLine($"- {item}"); return 1; }
var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet();
var existing = players.Where(player => player.PlayerPositions.Any(item => item.PositionId == position.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existing.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(value => value.PositionId != position.Id)).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Defensive Midfielder catalogue review: {resolved.Count} approved players, {existing.Count} current entries.");
Console.WriteLine($"Will remove Defensive Midfielder from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Defensive Midfielder to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }
await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(item => item.PositionId == position.Id));
foreach (var (target, player) in resolved) { if (player.PlayerPositions.All(item => item.PositionId != position.Id)) db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = position.Id, IsPrimary = false }); player.TransfermarktUrl = target.TransfermarktUrl; }
await db.SaveChangesAsync(); await transaction.CommitAsync(); Console.WriteLine("Defensive Midfielder catalogue and Transfermarkt links updated successfully."); return 0;
static bool Matches(Player player, string lookup) => string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) || player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));
sealed record Target(string Name, string Lookup, string TransfermarktUrl);
