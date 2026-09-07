using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Fabio Cannavaro", "Fabio Cannavaro", "https://www.transfermarkt.co.in/fabio-cannavaro/profil/spieler/5775"),
    new Target("Bobby Moore", "Bobby Moore", "https://www.transfermarkt.co.in/bobby-moore/profil/spieler/196086"),
    new Target("Alessandro Nesta", "Alessandro Nesta", "https://www.transfermarkt.co.in/alessandro-nesta/profil/spieler/4171"),
    new Target("Jürgen Kohler", "Jürgen Kohler", "https://www.transfermarkt.co.in/jurgen-kohler/profil/spieler/119"),
    new Target("Hans-Georg Schwarzenbeck", "Hans-Georg Schwarzenbeck", "https://www.transfermarkt.co.in/georg-schwarzenbeck/profil/spieler/72270"),
    new Target("Claudio Gentile", "Claudio Gentile", "https://www.transfermarkt.co.in/claudio-gentile/profil/spieler/128893"),
    new Target("Marcel Desailly", "Marcel Desailly", "https://www.transfermarkt.co.in/marcel-desailly/profil/spieler/3154"),
    new Target("Alessandro Costacurta", "Alessandro Costacurta", "https://www.transfermarkt.co.in/alessandro-costacurta/profil/spieler/10055"),
    new Target("Carles Puyol", "Puyol", "https://www.transfermarkt.co.in/carles-puyol/profil/spieler/7594"),
    new Target("Jaap Stam", "Stam", "https://www.transfermarkt.co.in/jaap-stam/profil/spieler/3557"),
    new Target("John Terry", "Terry", "https://www.transfermarkt.co.in/john-terry/profil/spieler/3160"),
    new Target("Nemanja Vidić", "Nemanja Vidić", "https://www.transfermarkt.co.in/nemanja-vidic-lrm-/profil/spieler/19726"),
    new Target("Sol Campbell", "Campbell", "https://www.transfermarkt.co.in/sol-campbell/profil/spieler/3198"),
    new Target("Giorgio Chiellini", "Chiellini", "https://www.transfermarkt.co.in/giorgio-chiellini/profil/spieler/29260"),
    new Target("José Nasazzi", "José Nasazzi", "https://www.transfermarkt.co.in/jose-nasazzi/profil/spieler/229598"),
    new Target("Karl-Heinz Förster", "Karl-Heinz Förster", "https://www.transfermarkt.co.in/karlheinz-forster/profil/spieler/65919")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var position = await db.Positions.SingleAsync(item => item.Code == "CBST");
var resolved = new List<(Target Target, Player Player)>(); var unresolved = new List<string>();
foreach (var target in targets)
{
    var matches = players.Where(player => Matches(player, target.Lookup)).ToList();
    if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)"); else resolved.Add((target, matches[0]));
}
if (unresolved.Count > 0) { Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:"); foreach (var item in unresolved) Console.Error.WriteLine($"- {item}"); return 1; }

var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet();
var existing = players.Where(player => player.PlayerPositions.Any(item => item.PositionId == position.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existing.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(value => value.PositionId != position.Id)).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();
Console.WriteLine($"Centre-Back (Stopper) catalogue review: {resolved.Count} approved players, {existing.Count} current entries.");
Console.WriteLine($"Will remove Centre-Back (Stopper) from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Centre-Back (Stopper) to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
if (!apply) { Console.WriteLine("Dry run complete. Re-run with --apply to save these changes."); return 0; }
await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals) db.PlayerPositions.Remove(player.PlayerPositions.Single(item => item.PositionId == position.Id));
foreach (var (target, player) in resolved) { if (player.PlayerPositions.All(item => item.PositionId != position.Id)) db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = position.Id, IsPrimary = false }); player.TransfermarktUrl = target.TransfermarktUrl; }
await db.SaveChangesAsync(); await transaction.CommitAsync(); Console.WriteLine("Centre-Back (Stopper) catalogue and Transfermarkt links updated successfully."); return 0;

static bool Matches(Player player, string lookup) => string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) || player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) || player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));
sealed record Target(string Name, string Lookup, string TransfermarktUrl);
