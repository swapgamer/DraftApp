using DraftDatastore.Domain.Entities;
namespace DraftDatastore.Application.Admin;
public sealed record AdminUserResponse(Guid Id,string Email,string DisplayName,bool IsActive,bool IsSystemAdmin,DateTimeOffset? AdminExpiresAtUtc,IReadOnlyCollection<string> Roles,DateTimeOffset CreatedAtUtc);
public sealed record TemporaryAdminRequest(DateTimeOffset? ExpiresAtUtc);
public sealed record DashboardResponse(int TotalUsers,int ActiveUsers,int TotalPlayers,int TotalChatQueries);
public sealed record LoginHistoryResponse(long Id,Guid UserId,string Email,bool Succeeded,string? IpAddress,DateTimeOffset OccurredAtUtc);
public sealed record AuditLogResponse(long Id,Guid? UserId,string Action,string EntityName,string EntityId,DateTimeOffset OccurredAtUtc);
public interface IAdminService { Task<IReadOnlyCollection<AdminUserResponse>> GetUsersAsync(CancellationToken ct); Task<AdminUserResponse?> GetUserAsync(Guid id,CancellationToken ct); Task<bool> SetUserStatusAsync(Guid actorId,Guid id,bool active,CancellationToken ct); Task<bool> GrantTemporaryAdminAsync(Guid actorId,Guid id,DateTimeOffset? expiresAtUtc,CancellationToken ct); Task<bool> RevokeTemporaryAdminAsync(Guid actorId,Guid id,CancellationToken ct); Task<DashboardResponse> GetDashboardAsync(CancellationToken ct); Task<IReadOnlyCollection<LoginHistoryResponse>> GetLoginHistoryAsync(CancellationToken ct); Task<IReadOnlyCollection<AuditLogResponse>> GetAuditLogsAsync(CancellationToken ct); Task<bool> ClearLoginHistoryAsync(Guid actorId,CancellationToken ct); Task<bool> ClearAuditLogsAsync(Guid actorId,CancellationToken ct); }
