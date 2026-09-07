using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Gerd Müller", "Bomber", 1, "https://www.transfermarkt.co.in/gerd-muller/profil/spieler/35604"),
    new Target("Pelé", "Edson Arantes", 1, "https://www.transfermarkt.co.in/pele/profil/spieler/17121"),
    new Target("Marco van Basten", "Marcel van Basten", 2, "https://www.transfermarkt.co.in/marco-van-basten/profil/spieler/74471"),
    new Target("Ronaldo Nazário", "R9", 2, "https://www.transfermarkt.co.in/ronaldo/profil/spieler/3140"),
    new Target("Puskás", "Ferenc Pusk", 3, "https://www.transfermarkt.co.in/ferenc-puskas/profil/spieler/103092"),
    new Target("Eusébio", "Eusébio", 4, "https://www.transfermarkt.co.in/eusebio/profil/spieler/89230"),
    new Target("Cristiano Ronaldo", "Cristiano Ronaldo", 4, "https://www.transfermarkt.co.in/cristiano-ronaldo/profil/spieler/8198", true),
    new Target("Romário", "Baixinho", 5, "https://www.transfermarkt.co.in/romario/profil/spieler/7942"),
    new Target("Josef Bican", "Josef Bican", 6, "https://www.transfermarkt.co.in/josef-bican/profil/spieler/237857"),
    new Target("Luis Suárez", "Luis Alberto", 6, "https://www.transfermarkt.co.in/luis-suarez/profil/spieler/44352"),
    new Target("Gabriel Batistuta", "Batigol", 6, "https://www.transfermarkt.co.in/gabriel-batistuta/profil/spieler/5959"),
    new Target("Karim Benzema", "KB9", 7, "https://www.transfermarkt.co.in/karim-benzema/profil/spieler/18922"),
    new Target("Robert Lewandowski", "Robert Lewandowski", 7, "https://www.transfermarkt.co.in/robert-lewandowski/profil/spieler/38253"),
    new Target("Paolo Rossi", "Paolo Rossi", 7, "https://www.transfermarkt.co.in/paolo-rossi/profil/spieler/116757"),
    new Target("Andriy Shevchenko", "Sheva", 8, "https://www.transfermarkt.co.in/andriy-shevchenko/profil/spieler/3522"),
    new Target("Filippo Inzaghi", "Filippo Inzaghi", 8, "https://www.transfermarkt.co.in/filippo-inzaghi/profil/spieler/5821"),
    new Target("Gary Lineker", "Lineker", 8, "https://www.transfermarkt.co.in/gary-lineker/profil/spieler/22256"),
    new Target("Samuel Eto'o", "Samuel Eto", 9, "https://www.transfermarkt.co.in/samuel-etoo/profil/spieler/4257"),
    new Target("Silvio Piola", "Silvio Piola", 9, "https://www.transfermarkt.co.in/silvio-piola/profil/spieler/177829"),
    new Target("Hernán Crespo", "Valdanito", 9, "https://www.transfermarkt.co.in/hernan-crespo/profil/spieler/3410"),
    new Target("Sándor Kocsis", "Golden Head", 10, "https://www.transfermarkt.co.in/sandor-kocsis/profil/spieler/136482"),
    new Target("David Villa", "David Villa", 10, "https://www.transfermarkt.co.in/david-villa/profil/spieler/7980")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var striker = await db.Positions.SingleAsync(position => position.Code == "ST");
var resolved = new List<(Target Target, Player Player)>();
var unresolved = new List<string>();

foreach (var target in targets)
{
    var matches = players.Where(player => Matches(player, target.Lookup)).ToList();
    if (matches.Count != 1) unresolved.Add($"{target.Name} ({matches.Count} matches)");
    else resolved.Add((target, matches[0]));
}

if (unresolved.Count > 0)
{
    Console.Error.WriteLine("No changes were made because these targets were not uniquely resolved:");
    foreach (var target in unresolved) Console.Error.WriteLine($"- {target}");
    return 1;
}

var approvedIds = resolved.Select(item => item.Player.Id).ToHashSet();
var currentStrikers = players.Where(player => player.PlayerPositions.Any(position => position.PositionId == striker.Id)).OrderBy(player => player.FullName).ToList();
var removals = currentStrikers.Where(player => !approvedIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(position => position.PositionId != striker.Id)).ToList();
var rankUpdates = resolved.Where(item => item.Player.PlayerPositions.SingleOrDefault(position => position.PositionId == striker.Id)?.OverallRank != item.Target.Rank).ToList();
var linkUpdates = resolved.Where(item => !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();

Console.WriteLine($"Striker catalogue review: {resolved.Count} approved players, {currentStrikers.Count} current entries.");
Console.WriteLine($"Will remove Striker from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Striker to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will set Striker-specific ranks for: {(rankUpdates.Count == 0 ? "none" : string.Join(", ", rankUpdates.Select(item => $"{item.Player.FullName} #{item.Target.Rank}")))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
Console.WriteLine("Exception entries retain all of their other position mappings.");

if (!apply)
{
    Console.WriteLine("Dry run complete. Re-run with --apply to save these changes.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals)
    db.PlayerPositions.Remove(player.PlayerPositions.Single(position => position.PositionId == striker.Id));

foreach (var (target, player) in resolved)
{
    var mapping = player.PlayerPositions.SingleOrDefault(position => position.PositionId == striker.Id);
    if (mapping is null)
    {
        mapping = new PlayerPosition { PlayerId = player.Id, PositionId = striker.Id, IsPrimary = false };
        db.PlayerPositions.Add(mapping);
    }

    mapping.OverallRank = target.Rank;
    player.TransfermarktUrl = target.TransfermarktUrl;
}

await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Striker catalogue, ranks, and Transfermarkt links updated successfully.");
return 0;

static bool Matches(Player player, string lookup) =>
    string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) ||
    player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));

sealed record Target(string Name, string Lookup, int Rank, string TransfermarktUrl, bool IsException = false);
