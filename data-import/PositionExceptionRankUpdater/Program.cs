using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

const string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True";
var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var targets = new[]
{
    new Target("Ruud Gullit; born Rudi Dil", "CMDLP", 5),
    new Target("Ruud Gullit; born Rudi Dil", "SS", 4),
    new Target("Lionel Andrés Messi Cuccittini", "SS", 3),
    new Target("Lionel Andrés Messi Cuccittini", "RW", 1),
    new Target("Cristiano Ronaldo dos Santos Aveiro", "ST", 4),
    new Target("Cristiano Ronaldo dos Santos Aveiro", "LW", 1),
    new Target("Franklin Edmundo Rijkaard", "CBST", 4),
    new Target("Franklin Edmundo Rijkaard", "DM", 1),
    new Target("Lothar Herbert Matthäus", "CMB2B", 1),
    new Target("Lothar Herbert Matthäus", "DM", 2),
    new Target("George Best", "RW", 3),
    new Target("George Best", "LW", 2)
};

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer(connectionString)
    .Options;
await using var db = new DraftDatastoreDbContext(options);

if (apply)
{
    await db.Database.MigrateAsync();
}

var players = await db.Players
    .Include(player => player.PlayerPositions)
    .ToListAsync();
var positions = await db.Positions.ToDictionaryAsync(position => position.Code);
var unresolved = targets.Where(target => !players.Any(player => player.FullName == target.PlayerName) || !positions.ContainsKey(target.PositionCode)).ToList();
if (unresolved.Count > 0)
{
    Console.Error.WriteLine("No changes were made because these exception targets could not be resolved:");
    foreach (var target in unresolved)
    {
        Console.Error.WriteLine($"- {target.PlayerName} / {target.PositionCode}");
    }

    return 1;
}

var changes = targets.Select(target =>
{
    var player = players.Single(item => item.FullName == target.PlayerName);
    var position = positions[target.PositionCode];
    var mapping = player.PlayerPositions.SingleOrDefault(item => item.PositionId == position.Id);
    return new Change(target, player, position, mapping);
}).ToList();

Console.WriteLine("Position-specific rank exception review:");
foreach (var change in changes)
{
    Console.WriteLine($"- {change.Player.FullName}: {change.Position.Name} #{change.Target.Rank}" +
        (change.Mapping is null ? " (position will be added)" : change.Mapping.OverallRank == change.Target.Rank ? " (already set)" : ""));
}

if (!apply)
{
    Console.WriteLine("Dry run complete. Re-run with --apply to migrate the database and save these ranks.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var change in changes)
{
    if (change.Mapping is null)
    {
        change.Mapping = new PlayerPosition
        {
            PlayerId = change.Player.Id,
            PositionId = change.Position.Id,
            IsPrimary = false
        };
        db.PlayerPositions.Add(change.Mapping);
    }

    change.Mapping.OverallRank = change.Target.Rank;
}

await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Position-specific exception ranks updated successfully.");
return 0;

sealed record Target(string PlayerName, string PositionCode, int Rank);

sealed class Change(Target target, Player player, Position position, PlayerPosition? mapping)
{
    public Target Target { get; } = target;
    public Player Player { get; } = player;
    public Position Position { get; } = position;
    public PlayerPosition? Mapping { get; set; } = mapping;
}
