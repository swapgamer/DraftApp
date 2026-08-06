using DraftDatastore.Domain.Entities;

namespace DraftDatastore.Application.Authentication;

public sealed class AuthService(IIdentityStore identityStore, IPasswordService passwordService, IJwtTokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (await identityStore.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var user = new User { Email = request.Email.Trim(), NormalizedEmail = normalizedEmail, DisplayName = request.DisplayName.Trim() };
        user.PasswordHash = passwordService.Hash(user, request.Password);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = SystemRoleIds.User, User = user });
        await identityStore.AddUserAsync(user, cancellationToken);
        var response = await IssueTokenPairAsync(user, [SystemRoles.User], ipAddress, cancellationToken);
        await identityStore.AddLoginHistoryAsync(user.Id, true, ipAddress, cancellationToken);
        await identityStore.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await identityStore.FindByNormalizedEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !user.IsActive || !passwordService.Verify(user, user.PasswordHash, request.Password))
        {
            if (user is not null) await identityStore.AddLoginHistoryAsync(user.Id, false, ipAddress, cancellationToken);
            await identityStore.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        var response = await IssueTokenPairAsync(user, roles, ipAddress, cancellationToken);
        await identityStore.AddLoginHistoryAsync(user.Id, true, ipAddress, cancellationToken);
        await identityStore.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var existing = await identityStore.FindRefreshTokenAsync(tokenService.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null || existing.ExpiresAtUtc <= DateTimeOffset.UtcNow || !existing.User.IsActive)
        {
            throw new UnauthorizedAccessException("The refresh token is invalid or expired.");
        }

        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        var roles = existing.User.UserRoles.Select(x => x.Role.Name).ToArray();
        var response = await IssueTokenPairAsync(existing.User, roles, ipAddress, cancellationToken);
        existing.ReplacedByTokenHash = tokenService.HashRefreshToken(response.RefreshToken);
        await identityStore.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken cancellationToken)
    {
        var token = await identityStore.FindRefreshTokenAsync(tokenService.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (token is not null && token.UserId == userId && token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            await identityStore.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthResponse> IssueTokenPairAsync(User user, IReadOnlyCollection<string> roles, string? ipAddress, CancellationToken cancellationToken)
    {
        var access = tokenService.CreateAccessToken(user, roles);
        var refreshToken = tokenService.CreateRefreshToken();
        await identityStore.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(refreshToken),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(14),
            CreatedByIpAddress = ipAddress
        }, cancellationToken);
        return new AuthResponse(access.Token, access.ExpiresAtUtc, refreshToken, new AuthenticatedUserResponse(user.Id, user.Email, user.DisplayName, roles));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
