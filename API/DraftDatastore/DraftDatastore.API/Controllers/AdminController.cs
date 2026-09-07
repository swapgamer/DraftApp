using System.Security.Claims;
using DraftDatastore.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DraftDatastore.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/v1/admin")]
public sealed class AdminController(IAdminService service) : ControllerBase
{
    [HttpGet("users")] public Task<IReadOnlyCollection<AdminUserResponse>> GetUsers(CancellationToken ct) => service.GetUsersAsync(ct);
    [HttpGet("users/{id:guid}")] public async Task<ActionResult<AdminUserResponse>> GetUser(Guid id, CancellationToken ct) => await service.GetUserAsync(id, ct) is { } user ? Ok(user) : NotFound();
    [HttpPatch("users/{id:guid}/status")] public async Task<IActionResult> SetStatus(Guid id, [FromQuery] bool active, CancellationToken ct) => await service.SetUserStatusAsync(CurrentUserId(), id, active, ct) ? NoContent() : Forbid();
    [HttpPost("users/{id:guid}/temporary-admin")] public async Task<IActionResult> GrantTemporaryAdmin(Guid id, TemporaryAdminRequest request, CancellationToken ct) => await service.GrantTemporaryAdminAsync(CurrentUserId(), id, request.ExpiresAtUtc, ct) ? NoContent() : Forbid();
    [HttpDelete("users/{id:guid}/temporary-admin")] public async Task<IActionResult> RevokeTemporaryAdmin(Guid id, CancellationToken ct) => await service.RevokeTemporaryAdminAsync(CurrentUserId(), id, ct) ? NoContent() : Forbid();
    [HttpGet("dashboard")] public Task<DashboardResponse> Dashboard(CancellationToken ct) => service.GetDashboardAsync(ct);
    [HttpGet("login-history")] public Task<IReadOnlyCollection<LoginHistoryResponse>> LoginHistory(CancellationToken ct) => service.GetLoginHistoryAsync(ct);
    [HttpGet("audit-logs")] public Task<IReadOnlyCollection<AuditLogResponse>> AuditLogs(CancellationToken ct) => service.GetAuditLogsAsync(ct);
    [HttpDelete("login-history")] public async Task<IActionResult> ClearLoginHistory(CancellationToken ct) => await service.ClearLoginHistoryAsync(CurrentUserId(), ct) ? NoContent() : Forbid();
    [HttpDelete("audit-logs")] public async Task<IActionResult> ClearAuditLogs(CancellationToken ct) => await service.ClearAuditLogsAsync(CurrentUserId(), ct) ? NoContent() : Forbid();
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
