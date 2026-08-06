using System.Text.Json;
using System.Text.RegularExpressions;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new DraftDatastoreDbContext(options);
var players = JsonSerializer.Deserialize<List<ImportPlayer>>(await File.ReadAllTextAsync("players.normalized.json")) ?? [];
await using var transaction = await db.Database.BeginTransactionAsync();
foreach (var item in players)
{
    var nationalityName = item.Nationality.Split('/')[0].Trim();
    var nationality = await db.Nationalities.FirstOrDefaultAsync(x => x.Name == nationalityName);
    if (nationality is null) { var code = Regex.Replace(nationalityName.ToUpperInvariant(), "[^A-Z]", "").PadRight(3, 'X')[..3]; if (await db.Nationalities.AnyAsync(x => x.IsoCode == code)) code = "X" + Guid.NewGuid().ToString("N")[..2].ToUpperInvariant(); nationality = new Nationality { Name = nationalityName, IsoCode = code }; db.Nationalities.Add(nationality); await db.SaveChangesAsync(); }
    var years = Regex.Matches(item.PlayingEra, "\\d{4}").Select(x => short.Parse(x.Value)).ToArray(); var start = years.FirstOrDefault(); var end = years.LastOrDefault(); if (end == 0) end = start; var eraName = $"{start}-{end}";
    var era = await db.PlayingEras.FirstOrDefaultAsync(x => x.Name == eraName); if (era is null) { era = new PlayingEra { Name = eraName, StartYear = start, EndYear = end }; db.PlayingEras.Add(era); await db.SaveChangesAsync(); }
    var existing = await db.Players.Include(x => x.PlayerPositions).FirstOrDefaultAsync(x => x.FullName == item.FullName);
    if (existing is not null) { foreach(var image in item.Images.Where(image => !db.PlayerImages.Any(x => x.PlayerId == existing.Id && x.BlobPath == image))) db.PlayerImages.Add(new PlayerImage{PlayerId=existing.Id,BlobPath=image,ContentType="image/jpeg",IsPrimary=!db.PlayerImages.Any(x=>x.PlayerId==existing.Id&&x.IsPrimary)}); await db.SaveChangesAsync(); continue; }
    var credits = Regex.Matches(item.Credits, "\\d+(?:\\.\\d+)?").Select(x => decimal.Parse(x.Value)).ToArray();
    var player = new Player { FullName = item.FullName[..Math.Min(160,item.FullName.Length)], NationalityId = nationality.Id, PlayingEraId = era.Id, ShortDescription = item.Description[..Math.Min(2000,item.Description.Length)], OverallRank = Math.Max(1,int.TryParse(Regex.Match(item.Rank,"\\d+").Value,out var rank)?rank:999), GoalCreditPoints = credits.ElementAtOrDefault(0), AssistCreditPoints = credits.ElementAtOrDefault(1), DefensiveCreditPoints = credits.ElementAtOrDefault(2), WikipediaUrl = Url(item.Links, "wikipedia"), TransfermarktUrl = Url(item.Links, "transfermarkt") };
    db.Players.Add(player); await db.SaveChangesAsync();
    foreach(var code in item.AllPositions.Distinct()) { var pos=await db.Positions.FirstOrDefaultAsync(x=>x.Code==code); if(pos is null){pos=new Position{Code=code,Name=code};db.Positions.Add(pos);await db.SaveChangesAsync();} db.PlayerPositions.Add(new PlayerPosition{PlayerId=player.Id,PositionId=pos.Id,IsPrimary=code==item.AllPositions.FirstOrDefault()}); }
    foreach(var alias in item.Aliases.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x is not "None" && x.Length>0)) db.PlayerAliases.Add(new PlayerAlias{PlayerId=player.Id,Alias=alias[..Math.Min(160,alias.Length)]});
    foreach(var image in item.Images) db.PlayerImages.Add(new PlayerImage{PlayerId=player.Id,BlobPath=image,ContentType=image.EndsWith(".jpeg",StringComparison.OrdinalIgnoreCase)?"image/jpeg":"image/jpeg",IsPrimary=image==item.Images.First()});
    await db.SaveChangesAsync();
}
await transaction.CommitAsync(); Console.WriteLine($"Imported {players.Count} source players; database now contains {await db.Players.CountAsync()} players.");
static string? Url(string input,string key)=>input.Split('/',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x=>x.Contains(key,StringComparison.OrdinalIgnoreCase)) is { } u ? "https://"+u : null;
sealed class ImportPlayer { [System.Text.Json.Serialization.JsonPropertyName("Full name")] public string FullName {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("Overall rank")] public string Rank {get;set;}=""; public string Nationality {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("Playing era")] public string PlayingEra {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("Short description")] public string ShortDescription {get;set;}=""; public string Description=>ShortDescription; [System.Text.Json.Serialization.JsonPropertyName("Goal, assist, and defensive credit points")] public string Credits {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("Optional aliases")] public string Aliases {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("Optional Wikipedia/Transfermarkt URLs")] public string Links {get;set;}=""; [System.Text.Json.Serialization.JsonPropertyName("allPositions")] public List<string> AllPositions {get;set;}=[]; [System.Text.Json.Serialization.JsonPropertyName("images")] public List<string> Images {get;set;}=[]; }
