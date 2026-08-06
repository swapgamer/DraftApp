using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
if (await db.ChemistryCombinations.AnyAsync()) { Console.WriteLine("Chemistry combinations already exist; seed skipped."); return; }
var players = await db.Players.Include(x => x.Aliases).ToListAsync();
var trios = new[]
{
    new[] { "Gullit", "Basten", "Rijkaard" }, new[] { "Xavi", "Iniesta", "Busquets" }, new[] { "Messi", "Xavi", "Iniesta" }, new[] { "Giggs", "Scholes", "Beckham" },
    new[] { "Laudrup", "Stoichkov", "Romario" }, new[] { "Cannavaro", "Pirlo", "Del Piero" }, new[] { "Ronaldo", "Zidane", "Figo" }, new[] { "Kohler", "Sammer", "Matth" },
    new[] { "Pele", "Garrincha", "Didi" }, new[] { "Koeman", "Laudrup", "Stoichkov" }, new[] { "Platini", "Tardelli", "Rossi" }, new[] { "Scirea", "Gentile", "Tardelli" },
    new[] { "Nordahl", "Liedholm", "Gren" }, new[] { "Cruyff", "Neeskens", "Krol" }, new[] { "Kaka", "Pirlo", "Seedorf" }
};
var duos = new[]
{
    new[] { "Platini", "Bonini" }, new[] { "Scirea", "Gentile" }, new[] { "Cruyff", "Neeskens" }, new[] { "Maldini", "Baresi" }, new[] { "Vieira", "Makelele" },
    new[] { "Kohler", "Sammer" }, new[] { "Xavi", "Iniesta" }, new[] { "Hierro", "Redondo" }, new[] { "Blanc", "Desailly" }, new[] { "Matth", "Rummenigge" },
    new[] { "Zico", "Socrates" }, new[] { "Carlos", "Cafu" }, new[] { "Zidane", "Henry" }, new[] { "Puskas", "Bozsik" }, new[] { "Kroos", "Modric" },
    new[] { "Gerson", "Rivelino" }, new[] { "Romario", "Bebeto" }, new[] { "Piola", "Meazza" }, new[] { "Maradona", "Buruchaga" }, new[] { "Maldini", "Nesta" },
    new[] { "Coluna", "Eusebio" }, new[] { "Messi", "Dani Alves" }, new[] { "Cristiano", "Marcelo" }, new[] { "Bican", "Sindelar" }
};
var created = 0; var skipped = new List<string>();
foreach (var (type, source) in trios.Select(x => ("trio", x)).Concat(duos.Select(x => ("duo", x))))
{
    var resolved = source.Select(Find).ToArray();
    if (resolved.Any(x => x is null) || resolved.Cast<Player?>().DistinctBy(x => x!.Id).Count() != source.Length) { skipped.Add(string.Join(" + ", source)); continue; }
    var group = new ChemistryCombination { Type = type };
    group.Players = resolved.Select((player, index) => new ChemistryCombinationPlayer { PlayerId = player!.Id, DisplayOrder = index + 1 }).ToList();
    db.ChemistryCombinations.Add(group); created++;
}
await db.SaveChangesAsync();
Console.WriteLine($"Created {created} combinations. Skipped: {(skipped.Count == 0 ? "none" : string.Join("; ", skipped))}");

Player? Find(string term)
{
    var exactAlias = players.Where(x => x.Aliases.Any(alias => string.Equals(alias.Alias, term, StringComparison.OrdinalIgnoreCase))).ToList();
    if (exactAlias.Count == 1) return exactAlias[0];
    var fullName = players.Where(x => x.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    if (fullName.Count == 1) return fullName[0];
    var alias = players.Where(x => x.Aliases.Any(value => value.Alias.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
    return alias.Count == 1 ? alias[0] : null;
}
