using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Cafu", "Cafu", "https://www.transfermarkt.co.in/cafu/profil/spieler/5937"),
    new Target("Carlos Alberto Torres", "Carlos Alberto Torres", "https://www.transfermarkt.co.in/carlos-alberto-torres/profil/spieler/229662"),
    new Target("Djalma Santos", "Djalma Santos", "https://www.transfermarkt.co.in/djalma-santos/profil/spieler/137269"),
    new Target("Lilian Thuram", "Lilian Thuram", "https://www.transfermarkt.co.in/lilian-thuram/profil/spieler/3521"),
    new Target("Giuseppe Bergomi", "Giuseppe Bergomi", "https://www.transfermarkt.co.in/giuseppe-bergomi/profil/spieler/107860"),
    new Target("Javier Zanetti", "Zanetti", "https://www.transfermarkt.co.in/javier-zanetti/profil/spieler/1161"),
    new Target("Philipp Lahm", "Philipp Lahm", "https://www.transfermarkt.co.in/philipp-lahm/profil/spieler/2219"),
    new Target("Dani Alves", "Dani Alves", "https://www.transfermarkt.co.in/dani-alves/profil/spieler/15951"),
    new Target("Berti Vogts", "Vogts", "https://www.transfermarkt.co.in/berti-vogts/profil/spieler/72839"),
    new Target("Tarcisio Burgnich", "Tarcisio Burgnich", "https://www.transfermarkt.co.in/tarcisio-burgnich/profil/spieler/145342"),
    new Target("Mauro Tassotti", "Mauro Tassotti", "https://www.transfermarkt.co.in/mauro-tassotti/profil/spieler/102483"),
    new Target("Gianluca Zambrotta", "Gianluca Zambrotta", "https://www.transfermarkt.co.in/gianluca-zambrotta/profil/spieler/5757"),
    new Target("Wim Suurbier", "Wim Suurbier", "https://www.transfermarkt.co.in/wim-suurbier/profil/spieler/95153"),
    new Target("Manuel Amoros", "Manuel Amoros", "https://www.transfermarkt.co.in/manuel-amoros/profil/spieler/101108")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var rightBack = await db.Positions.SingleAsync(position => position.Code == "RB");
var resolved = new List<(Target Target, Player Player)>();
var unresolved = new List<string>();

foreach (var target in targets)
{
    var matches = players.Where(player => Matches(player, target.Lookup)).ToList();
    if (matches.Count != 1)
    {
        unresolved.Add($"{target.Name} ({matches.Count} matches)");
        continue;
    }
    resolved.Add((target, matches[0]));
}

if (unresolved.Count > 0)
{
    Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:");
    foreach (var item in unresolved) Console.Error.WriteLine($"- {item}");
    return 1;
}

var allowedPlayerIds = resolved.Select(item => item.Player.Id).ToHashSet();
var existingRightBacks = players.Where(player => player.PlayerPositions.Any(position => position.PositionId == rightBack.Id)).OrderBy(player => player.OverallRank).ToList();
var removals = existingRightBacks.Where(player => !allowedPlayerIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(position => position.PositionId != rightBack.Id)).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();

Console.WriteLine($"Right-back catalogue review: {resolved.Count} approved players, {existingRightBacks.Count} current entries.");
Console.WriteLine($"Will remove Right-Back from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Right-Back to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");

if (!apply)
{
    Console.WriteLine("Dry run complete. Re-run with --apply to save these changes.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals)
{
    var position = player.PlayerPositions.Single(item => item.PositionId == rightBack.Id);
    db.PlayerPositions.Remove(position);
}
foreach (var (target, player) in resolved)
{
    if (player.PlayerPositions.All(position => position.PositionId != rightBack.Id))
        db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = rightBack.Id, IsPrimary = false });
    player.TransfermarktUrl = target.TransfermarktUrl;
}
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Right-back catalogue and Transfermarkt links updated successfully.");

return 0;

static bool Matches(Player player, string target) =>
    string.Equals(player.FullName, target, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => string.Equals(alias.Alias, target, StringComparison.OrdinalIgnoreCase)) ||
    player.FullName.Contains(target, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => alias.Alias.Contains(target, StringComparison.OrdinalIgnoreCase));

sealed record Target(string Name, string Lookup, string TransfermarktUrl);
