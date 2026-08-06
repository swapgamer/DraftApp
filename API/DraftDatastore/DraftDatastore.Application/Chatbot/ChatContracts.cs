using FluentValidation;
namespace DraftDatastore.Application.Chatbot;
public sealed record ChatQueryRequest(string Message);
public sealed record ChatPlayerResult(Guid Id,string Name,int Rank,string Position,decimal GoalCredit,decimal AssistCredit,decimal DefensiveCredit);
public sealed record ChatResponse(string Intent,string Answer,bool NoDataFound,IReadOnlyCollection<ChatPlayerResult> Players);
public interface IChatbotService { Task<ChatResponse> QueryAsync(Guid userId,ChatQueryRequest request,CancellationToken ct); }
public sealed class ChatQueryRequestValidator:AbstractValidator<ChatQueryRequest>{public ChatQueryRequestValidator(){RuleFor(x=>x.Message).NotEmpty().MaximumLength(500);}}
