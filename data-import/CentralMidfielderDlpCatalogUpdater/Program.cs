using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Xavi", "Xavi", "https://www.transfermarkt.co.in/xavi/profil/spieler/7607"),
    new Target("Didi", "Valdir Pereira", "https://www.transfermarkt.co.in/didi/profil/spieler/137264"),
    new Target("Andrés Iniesta", "Andrés Iniesta Luján", "https://www.transfermarkt.co.in/andres-iniesta/profil/spieler/7600"),
    new Target("Andrea Pirlo", "Andrea Pirlo", "https://www.transfermarkt.co.in/andrea-pirlo/profil/spieler/5817"),
    new Target("Luis Suárez Miramontes", "Luis Suárez Miramontes", "https://www.transfermarkt.co.in/luis-suarez/profil/spieler/172315"),
    new Target("Luka Modrić", "Luka Modrić", "https://www.transfermarkt.co.in/luka-modric/profil/spieler/27992"),
    new Target("Toni Kroos", "Toni Kroos", "https://www.transfermarkt.co.in/toni-kroos/profil/spieler/31909"),
    new Target("Ruud Gullit", "Ruud Gullit; born Rudi Dil", "https://www.transfermarkt.co.in/ruud-gullit/profil/spieler/101045"),
    new Target("Paulo Roberto Falcão", "Paulo Roberto Falcão", "https://www.transfermarkt.co.in/falcao/profil/spieler/117621"),
    new Target("Gérson", "Gérson de Oliveira Nunes", "https://www.transfermarkt.co.in/gerson/profil/spieler/229676"),
    new Target("Günter Netzer", "Günter Theo Netzer", "https://www.transfermarkt.co.in/gunter-netzer/profil/spieler/24690"),
    new Target("Willem van Hanegem", "Willem van Hanegem", "https://www.transfermarkt.co.in/willem-van-hanegem/profil/spieler/142884"),
    new Target("David Silva", "David Josué Jiménez Silva", "https://www.transfermarkt.co.in/david-silva/profil/spieler/35518"),
    new Target("Nils Liedholm", "Nils Erik Liedholm", "https://www.transfermarkt.co.in/nils-liedholm/profil/spieler/174158")
};
var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var position = await db.Positions.SingleAsync(item => item.Code == "CMDLP");
var resolved = new List<(Target Target, Player Player)>(); var unresolved = new List<string>();
foreach (var target in targets) { var matches = players.Where(player => Matches(player, target.Lookup)).ToList(); if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)"); else resolved.Add((target, matches[0])); }
if (unresolved.Count > 0) { Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:"); foreach (var item in unresolved) Console.Error.WriteLine($"- {item}"); return 1; }
var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet(); var existing = players.Where(player => player.PlayerPositions.Any(item => item.PositionId == position.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existing.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList(); var additions = resolved.Where(item => item.Player.PlayerPositions.All(value => value.PositionId != position.Id)).ToList(); var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Central Midfielder (DLP) catalogue review: {resolved.Count} approved players, {existing.Count} current entries.");
Console.WriteLine($"Will remove Central Midfielder (DLP) from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Central Midfielder (DLP) to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }
await using var transaction = await db.Database.BeginTransactionAsync(); foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(item => item.PositionId == position.Id)); foreach (var (target, player) in resolved) { if (player.PlayerPositions.All(item => item.PositionId != position.Id)) db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = position.Id, IsPrimary = false }); player.TransfermarktUrl = target.TransfermarktUrl; } await db.SaveChangesAsync(); await transaction.CommitAsync(); Console.WriteLine("Central Midfielder (DLP) catalogue and Transfermarkt links updated successfully."); return 0;
static bool Matches(Player player, string lookup) => string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) || player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));
sealed record Target(string Name, string Lookup, string TransfermarktUrl);
