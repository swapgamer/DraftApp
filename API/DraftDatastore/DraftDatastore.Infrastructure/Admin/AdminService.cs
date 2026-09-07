using System.Text.Json;
using DraftDatastore.Application.Admin;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.Infrastructure.Admin;

public sealed class AdminService(DraftDatastoreDbContext db) : IAdminService
{
    public async Task<IReadOnlyCollection<AdminUserResponse>> GetUsersAsync(CancellationToken ct)
    {
        var users = await UserQuery().OrderBy(x => x.Email).ToListAsync(ct);
        return users.Select(ToResponse).ToArray();
    }

    public async Task<AdminUserResponse?> GetUserAsync(Guid id, CancellationToken ct)
    {
        var user = await UserQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        return user is null ? null : ToResponse(user);
    }

    public async Task<bool> SetUserStatusAsync(Guid actorId, Guid id, bool active, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || user.IsSystemAdmin) return false;
        user.IsActive = active;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GrantTemporaryAdminAsync(Guid actorId, Guid id, DateTimeOffset? expiresAtUtc, CancellationToken ct)
    {
        if (expiresAtUtc is not null && expiresAtUtc <= DateTimeOffset.UtcNow) return false;
        var actor = await db.Users.SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var user = await db.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (actor?.IsSystemAdmin != true || user is null || user.IsSystemAdmin) return false;
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = id, RoleId = SystemRoleIds.Admin });
        user.AdminExpiresAtUtc = expiresAtUtc;
        AddAudit(actorId, "GrantTemporaryAdmin", id, new { expiresAtUtc });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RevokeTemporaryAdminAsync(Guid actorId, Guid id, CancellationToken ct)
    {
        var actor = await db.Users.SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var user = await db.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (actor?.IsSystemAdmin != true || user is null || user.IsSystemAdmin) return false;
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = id, RoleId = SystemRoleIds.User });
        user.AdminExpiresAtUtc = null;
        AddAudit(actorId, "RevokeTemporaryAdmin", id, null);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<DashboardResponse> GetDashboardAsync(CancellationToken ct) => Task.FromResult(new DashboardResponse(db.Users.Count(), db.Users.Count(x => x.IsActive), db.Players.Count(), db.ChatHistories.Count()));
    public async Task<IReadOnlyCollection<LoginHistoryResponse>> GetLoginHistoryAsync(CancellationToken ct) => await db.LoginHistories.Include(x => x.User).AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(200).Select(x => new LoginHistoryResponse(x.Id, x.UserId, x.User.Email, x.Succeeded, x.IpAddress, x.OccurredAtUtc)).ToListAsync(ct);
    public async Task<IReadOnlyCollection<AuditLogResponse>> GetAuditLogsAsync(CancellationToken ct) => await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(200).Select(x => new AuditLogResponse(x.Id, x.UserId, x.Action, x.EntityName, x.EntityId, x.OccurredAtUtc)).ToListAsync(ct);

    public async Task<bool> ClearLoginHistoryAsync(Guid actorId, CancellationToken ct)
    {
        if (!await IsSystemAdminAsync(actorId, ct)) return false;
        await db.LoginHistories.ExecuteDeleteAsync(ct);
        AddAudit(actorId, "ClearLoginHistory", Guid.Empty, null);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ClearAuditLogsAsync(Guid actorId, CancellationToken ct)
    {
        if (!await IsSystemAdminAsync(actorId, ct)) return false;
        await db.AuditLogs.ExecuteDeleteAsync(ct);
        AddAudit(actorId, "ClearAuditLogs", Guid.Empty, null);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<User> UserQuery() => db.Users
        .Include(x => x.UserRoles)
        .ThenInclude(x => x.Role)
        .AsNoTracking();

    private static AdminUserResponse ToResponse(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.IsSystemAdmin,
        user.AdminExpiresAtUtc,
        user.UserRoles.Select(x => x.Role.Name).ToArray(),
        user.CreatedAtUtc);

    private Task<bool> IsSystemAdminAsync(Guid actorId, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == actorId && x.IsSystemAdmin, ct);
    private void AddAudit(Guid actorId, string action, Guid targetId, object? changes) => db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = action, EntityName = "User", EntityId = targetId.ToString(), ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes), OccurredAtUtc = DateTimeOffset.UtcNow });
}
