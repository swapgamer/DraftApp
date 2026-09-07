using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Johan Cruyff", "Jopie", 1, "https://www.transfermarkt.co.in/johan-cruyff/profil/spieler/8021"),
    new Target("Karl-Heinz Rummenigge", "Kalle", 2, "https://www.transfermarkt.co.in/karl-heinz-rummenigge/profil/spieler/72343"),
    new Target("Roberto Baggio", "Roberto Baggio", 3, "https://www.transfermarkt.co.in/roberto-baggio/profil/spieler/4153"),
    new Target("Lionel Messi", "Lionel Andrés Messi", 3, null, true),
    new Target("Giuseppe Meazza", "Giuseppe Meazza", 4, "https://www.transfermarkt.co.in/giuseppe-meazza/profil/spieler/183779"),
    new Target("Ruud Gullit", "The Black Tulip", 4, "https://www.transfermarkt.co.in/ruud-gullit/profil/spieler/101045", true),
    new Target("Alessandro Del Piero", "ADP", 5, "https://www.transfermarkt.co.in/alessandro-del-piero/profil/spieler/4289"),
    new Target("Kevin Keegan", "King Kev", 5, "https://www.transfermarkt.co.in/kevin-keegan/profil/spieler/85458"),
    new Target("Dennis Bergkamp", "The Iceman", 5, "https://www.transfermarkt.co.in/dennis-bergkamp/profil/spieler/3187"),
    new Target("Eric Cantona", "King Eric", 6, "https://www.transfermarkt.co.in/eric-cantona/profil/spieler/12000"),
    new Target("Raúl", "González Blanco", 6, "https://www.transfermarkt.co.in/raul/profil/spieler/7349"),
    new Target("Wayne Rooney", "Wazza", 7, "https://www.transfermarkt.co.in/wayne-rooney/profil/spieler/3332"),
    new Target("Omar Sívori", "Omar Sívori", 7, "https://www.transfermarkt.co.in/omar-sivori/profil/spieler/50816"),
    new Target("Francesco Totti", "Francesco Totti", 7, "https://www.transfermarkt.co.in/francesco-totti/profil/spieler/5958"),
    new Target("Kenny Dalglish", "King Kenny", 7, "https://www.transfermarkt.co.in/kenny-dalglish/profil/spieler/135269"),
    new Target("José Manuel Moreno", "JMM", 8, "https://www.transfermarkt.co.in/jose-manuel-moreno/profil/spieler/510184"),
    new Target("Igor Belanov", "Igor Ivanovich Belanov", 8, "https://www.transfermarkt.co.in/igor-belanov/profil/spieler/85752"),
    new Target("Mario Kempes", "El Matador", 8, "https://www.transfermarkt.co.in/mario-kempes/profil/spieler/37264"),
    new Target("Flórián Albert", "The Emperor", 9, "https://www.transfermarkt.co.in/florian-albert/profil/spieler/151244"),
    new Target("Matthias Sindelar", "The Paper Man", 9, "https://www.transfermarkt.co.in/matthias-sindelar/profil/spieler/129716"),
    new Target("László Kubala", "Laszi", 10, "https://www.transfermarkt.co.in/laszlo-kubala/profil/spieler/156145")
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var db = new DraftDatastoreDbContext(options);
var players = await db.Players.Include(player => player.Aliases).Include(player => player.PlayerPositions).ToListAsync();
var secondStriker = await db.Positions.SingleAsync(position => position.Code == "SS");
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
var currentEntries = players.Where(player => player.PlayerPositions.Any(position => position.PositionId == secondStriker.Id)).OrderBy(player => player.FullName).ToList();
var removals = currentEntries.Where(player => !approvedIds.Contains(player.Id)).ToList();
var additions = resolved.Where(item => item.Player.PlayerPositions.All(position => position.PositionId != secondStriker.Id)).ToList();
var rankUpdates = resolved.Where(item => item.Player.PlayerPositions.SingleOrDefault(position => position.PositionId == secondStriker.Id)?.OverallRank != item.Target.Rank).ToList();
var linkUpdates = resolved.Where(item => item.Target.TransfermarktUrl is not null && !string.Equals(item.Player.TransfermarktUrl, item.Target.TransfermarktUrl, StringComparison.Ordinal)).ToList();

Console.WriteLine($"Second Striker catalogue review: {resolved.Count} approved players, {currentEntries.Count} current entries.");
Console.WriteLine($"Will remove Second Striker from: {(removals.Count == 0 ? "none" : string.Join(", ", removals.Select(player => player.FullName)))}");
Console.WriteLine($"Will add Second Striker to: {(additions.Count == 0 ? "none" : string.Join(", ", additions.Select(item => item.Player.FullName)))}");
Console.WriteLine($"Will set Second Striker-specific ranks for: {(rankUpdates.Count == 0 ? "none" : string.Join(", ", rankUpdates.Select(item => $"{item.Player.FullName} #{item.Target.Rank}")))}");
Console.WriteLine($"Will update Transfermarkt links for: {(linkUpdates.Count == 0 ? "none" : string.Join(", ", linkUpdates.Select(item => item.Player.FullName)))}");
Console.WriteLine("Exception entries retain all of their other position mappings.");

if (!apply)
{
    Console.WriteLine("Dry run complete. Re-run with --apply to save these changes.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var player in removals)
    db.PlayerPositions.Remove(player.PlayerPositions.Single(position => position.PositionId == secondStriker.Id));

foreach (var (target, player) in resolved)
{
    var mapping = player.PlayerPositions.SingleOrDefault(position => position.PositionId == secondStriker.Id);
    if (mapping is null)
    {
        mapping = new PlayerPosition { PlayerId = player.Id, PositionId = secondStriker.Id, IsPrimary = false };
        db.PlayerPositions.Add(mapping);
    }

    mapping.OverallRank = target.Rank;
    if (target.TransfermarktUrl is not null) player.TransfermarktUrl = target.TransfermarktUrl;
}

await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Second Striker catalogue, ranks, and Transfermarkt links updated successfully.");
return 0;

static bool Matches(Player player, string lookup) =>
    string.Equals(player.FullName, lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => string.Equals(alias.Alias, lookup, StringComparison.OrdinalIgnoreCase)) ||
    player.FullName.Contains(lookup, StringComparison.OrdinalIgnoreCase) ||
    player.Aliases.Any(alias => alias.Alias.Contains(lookup, StringComparison.OrdinalIgnoreCase));

sealed record Target(string Name, string Lookup, int Rank, string? TransfermarktUrl, bool IsException = false);
