using System.Text.Json;
using System.Text.Json.Serialization;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var db = new DraftDatastoreDbContext(options);
var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "players.normalized.json"));
var players = JsonSerializer.Deserialize<List<SourcePlayer>>(await File.ReadAllTextAsync(sourcePath)) ?? [];

var catalog = new[]
{
    ("GK", "Goalkeeper"), ("RB", "Right-Back"), ("LB", "Left-Back"),
    ("CBST", "Center-Back (Stopper)"), ("CBSW", "Center-Back (Sweeper)"),
    ("DM", "Defensive Midfielder"), ("CMDLP", "Central Midfielder (DLP)"),
    ("CMB2B", "Central Midfielder (B2B)"), ("CAM", "Central Attacking Midfielder"),
    ("RW", "Right Wing"), ("LW", "Left Wing"), ("SS", "Second Striker"), ("ST", "Striker")
};
var folderToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["CAM"] = "CAM", ["CB_Stopper"] = "CBST", ["CB_Sweeper"] = "CBSW", ["CDM"] = "DM",
    ["CF"] = "ST", ["CM_B2B"] = "CMB2B", ["CM_DLP"] = "CMDLP", ["LB"] = "LB",
    ["LW"] = "LW", ["RB"] = "RB", ["RW"] = "RW", ["SS"] = "SS"
};
var secondaryToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["AM"] = "CAM", ["CB"] = "CBST", ["CF"] = "ST", ["CM"] = "CMDLP",
    ["LB"] = "LB", ["LW"] = "LW", ["RB"] = "RB", ["RW"] = "RW", ["SS"] = "SS"
};
var curatedOverrides = new Dictionary<string, (string PrimaryCode, int PositionRank)>(StringComparer.OrdinalIgnoreCase)
{
    ["Cristiano Ronaldo dos Santos Aveiro"] = ("LW", 1),
    ["Lothar Herbert Matthäus"] = ("CMB2B", 1)
};

await using var transaction = await db.Database.BeginTransactionAsync();
var existing = await db.Positions.ToListAsync();
foreach (var (code, name) in catalog)
{
    var position = existing.SingleOrDefault(x => x.Code == code);
    if (position is null) { position = new Position { Code = code, Name = name }; db.Positions.Add(position); existing.Add(position); }
    else position.Name = name;
}
await db.SaveChangesAsync();

var byCode = (await db.Positions.ToListAsync()).ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
var databasePlayers = await db.Players.Include(x => x.PlayerPositions).ToDictionaryAsync(x => x.FullName);
var messi = databasePlayers.Values.SingleOrDefault(x => x.FullName.Contains("Messi", StringComparison.OrdinalIgnoreCase));
if (messi is not null)
{
    var activeEra = await db.PlayingEras.SingleOrDefaultAsync(x => x.StartYear == 2004 && x.EndYear == 2026);
    if (activeEra is null)
    {
        activeEra = new PlayingEra { Name = "2004-Present", StartYear = 2004, EndYear = 2026 };
        db.PlayingEras.Add(activeEra);
    }
    messi.PlayingEra = activeEra;
}
db.PlayerPositions.RemoveRange(await db.PlayerPositions.ToListAsync());
foreach (var source in players)
{
    if (!databasePlayers.TryGetValue(source.FullName, out var player) || !folderToCode.TryGetValue(source.Folder, out var primaryCode)) continue;
    if (curatedOverrides.TryGetValue(source.FullName, out var curated)) { primaryCode = curated.PrimaryCode; player.OverallRank = curated.PositionRank; }
    var codes = source.AllPositions.Select(x => secondaryToCode.TryGetValue(x, out var code) ? code : null).Where(x => x is not null).Cast<string>().Append(primaryCode).Distinct();
    foreach (var code in codes) db.PlayerPositions.Add(new PlayerPosition { PlayerId = player.Id, PositionId = byCode[code].Id, IsPrimary = code == primaryCode });
}
await db.SaveChangesAsync();

var officialCodes = catalog.Select(x => x.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
var unused = await db.Positions.Where(x => !officialCodes.Contains(x.Code)).ToListAsync();
db.Positions.RemoveRange(unused);
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine($"Position catalogue updated: {catalog.Length} positions, {players.Count} source players mapped.");

sealed class SourcePlayer
{
    [JsonPropertyName("Full name")] public string FullName { get; set; } = "";
    [JsonPropertyName("folder")] public string Folder { get; set; } = "";
    [JsonPropertyName("allPositions")] public List<string> AllPositions { get; set; } = [];
}
