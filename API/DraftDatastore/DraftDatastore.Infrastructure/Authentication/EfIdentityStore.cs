using DraftDatastore.Application.Authentication;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.Infrastructure.Authentication;

public sealed class EfIdentityStore(DraftDatastoreDbContext dbContext) : IIdentityStore
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => dbContext.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) => dbContext.RefreshTokens.Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task AddUserAsync(User user, CancellationToken cancellationToken) => dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken) => dbContext.RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    public Task AddLoginHistoryAsync(Guid userId, bool succeeded, string? ipAddress, CancellationToken cancellationToken) => dbContext.LoginHistories.AddAsync(new LoginHistory { UserId = userId, Succeeded = succeeded, IpAddress = ipAddress, OccurredAtUtc = DateTimeOffset.UtcNow }, cancellationToken).AsTask();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
