using System.Security.Claims;using DraftDatastore.Application.Chatbot;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.AspNetCore.RateLimiting;
namespace DraftDatastore.API.Controllers;
[ApiController][Authorize][Route("api/v1/chatbot")]
public sealed class ChatbotController(IChatbotService service):ControllerBase { [HttpPost("query")][EnableRateLimiting("chat")] public Task<ChatResponse> Query(ChatQueryRequest request,CancellationToken ct)=>service.QueryAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),request,ct); }

