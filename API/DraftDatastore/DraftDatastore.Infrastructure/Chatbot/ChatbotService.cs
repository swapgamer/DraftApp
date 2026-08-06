using System.Text.Json;
using DraftDatastore.Application.Chatbot;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.Infrastructure.Chatbot;

public sealed class ChatbotService(DraftDatastoreDbContext db) : IChatbotService
{
    private static readonly string[] GoalkeeperCodes = ["GK"];
    private static readonly string[] CentreBackCodes = ["CBST", "CBSW"];
    private static readonly string[] StrikerCodes = ["ST"];
    private static readonly string[] AttackingMidfielderCodes = ["CAM"];
    private static readonly string[] CentralMidfielderCodes = ["CMDLP", "CMB2B"];
    private static readonly string[] DefensiveMidfielderCodes = ["DM"];
    private static readonly string[] RightBackCodes = ["RB"];
    private static readonly string[] LeftBackCodes = ["LB"];
    private static readonly string[] RightWingCodes = ["RW"];
    private static readonly string[] LeftWingCodes = ["LW"];
    private static readonly string[] SecondStrikerCodes = ["SS"];
    public async Task<ChatResponse> QueryAsync(Guid userId, ChatQueryRequest request, CancellationToken ct)
    {
        var started = Environment.TickCount64;
        var text = request.Message.Trim();
        var lower = text.ToLowerInvariant();
        var query = db.Players.AsNoTracking().Include(x => x.PlayerPositions).ThenInclude(x => x.Position).AsQueryable();
        string intent;
        List<Player> players;

        var positionCode = FindPositionCode(lower);
        if (lower.Contains("top") && positionCode is not null)
        {
            intent = "TopByPosition";
            players = await query.Where(x => x.PlayerPositions.Any(p => positionCode.Contains(p.Position.Code))).OrderBy(x => x.OverallRank).Take(5).ToListAsync(ct);
        }
        else if (lower.Contains("rank") && positionCode is not null)
        {
            intent = "RankByPosition";
            players = await query.Where(x => x.PlayerPositions.Any(p => positionCode.Contains(p.Position.Code))).OrderBy(x => x.OverallRank).Take(1).ToListAsync(ct);
        }
        else if (lower.Contains("highest goal credit"))
        {
            intent = "HighestGoalCredit";
            players = await query.OrderByDescending(x => x.GoalCreditPoints).Take(5).ToListAsync(ct);
        }
        else if (lower.Contains("highest defensive credit"))
        {
            intent = "HighestDefensiveCredit";
            players = await query.OrderByDescending(x => x.DefensiveCreditPoints).Take(5).ToListAsync(ct);
        }
        else
        {
            intent = "PlayerLookup";
            var terms = lower.Replace("who is ", string.Empty).Replace("who's ", string.Empty).Replace("tell me about ", string.Empty).Trim();
            players = await query.Where(x => EF.Functions.Like(x.FullName, "%" + terms + "%") || x.Aliases.Any(a => EF.Functions.Like(a.Alias, "%" + terms + "%"))).OrderBy(x => x.OverallRank).Take(5).ToListAsync(ct);
        }

        var results = players.Select(x => new ChatPlayerResult(x.Id, x.FullName, x.OverallRank, (x.PlayerPositions.FirstOrDefault(p => p.IsPrimary) ?? x.PlayerPositions.FirstOrDefault())?.Position.Name ?? "Unknown", x.GoalCreditPoints, x.AssistCreditPoints, x.DefensiveCreditPoints)).ToArray();
        var answer = results.Length == 0 ? "No matching players were found in the current datastore." : $"Found {results.Length} database result(s).";
        var response = new ChatResponse(intent, answer, results.Length == 0, results);
        db.ChatHistories.Add(new ChatHistory { UserId = userId, Message = text, Intent = intent, ResponseJson = JsonSerializer.Serialize(response), HasMatch = !response.NoDataFound, DurationMilliseconds = Environment.TickCount64 - started });
        await db.SaveChangesAsync(ct);
        return response;
    }

    private static string[]? FindPositionCode(string text) =>
        text.Contains("goalkeeper") || text.Contains("gk") ? GoalkeeperCodes :
        text.Contains("centre back") || text.Contains("center back") || text.Contains("cb") ? CentreBackCodes :
        text.Contains("second striker") || text.Contains("ss") ? SecondStrikerCodes :
        text.Contains("striker") || text.Contains("centre forward") || text.Contains("center forward") || text.Contains("cf") ? StrikerCodes :
        text.Contains("defensive midfielder") || text.Contains("dm") ? DefensiveMidfielderCodes :
        text.Contains("attacking midfielder") || text.Contains("cam") ? AttackingMidfielderCodes :
        text.Contains("central midfielder") || text.Contains("cm") ? CentralMidfielderCodes :
        text.Contains("left back") || text.Contains("lb") ? LeftBackCodes :
        text.Contains("right back") || text.Contains("rb") ? RightBackCodes :
        text.Contains("left wing") || text.Contains("lw") ? LeftWingCodes :
        text.Contains("right wing") || text.Contains("rw") ? RightWingCodes : null;
}
