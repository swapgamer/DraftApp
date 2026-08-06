using DraftDatastore.Domain.Entities;

namespace DraftDatastore.Application.Authentication;

public interface IIdentityStore
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken);
    Task AddLoginHistoryAsync(Guid userId, bool succeeded, string? ipAddress, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string suppliedPassword);
}

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(User user, IReadOnlyCollection<string> roles);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
