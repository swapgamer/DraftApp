using DraftDatastore.Application.Admin;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace DraftDatastore.API.Controllers;
[ApiController][Authorize(Roles="Admin")][Route("api/v1/admin")]
public sealed class AdminController(IAdminService service):ControllerBase {
 [HttpGet("users")] public Task<IReadOnlyCollection<AdminUserResponse>> GetUsers(CancellationToken ct)=>service.GetUsersAsync(ct);
 [HttpGet("users/{id:guid}")] public async Task<ActionResult<AdminUserResponse>> GetUser(Guid id,CancellationToken ct){var user=await service.GetUserAsync(id,ct);return user is null?NotFound():Ok(user);}
 [HttpPatch("users/{id:guid}/status")] public async Task<IActionResult> SetStatus(Guid id,[FromQuery] bool active,CancellationToken ct)=>await service.SetUserStatusAsync(id,active,ct)?NoContent():NotFound();
 [HttpPut("users/{id:guid}/role")] public async Task<IActionResult> ChangeRole(Guid id,RoleChangeRequest request,CancellationToken ct)=>await service.ChangeRoleAsync(id,request.RoleId,ct)?NoContent():NotFound();
 [HttpGet("dashboard")] public Task<DashboardResponse> Dashboard(CancellationToken ct)=>service.GetDashboardAsync(ct);
 [HttpGet("login-history")] public Task<IReadOnlyCollection<LoginHistoryResponse>> LoginHistory(CancellationToken ct)=>service.GetLoginHistoryAsync(ct);
 [HttpGet("audit-logs")] public Task<IReadOnlyCollection<AuditLogResponse>> AuditLogs(CancellationToken ct)=>service.GetAuditLogsAsync(ct);
}
