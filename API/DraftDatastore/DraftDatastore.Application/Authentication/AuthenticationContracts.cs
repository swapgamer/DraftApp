using FluentValidation;

namespace DraftDatastore.Application.Authentication;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthenticatedUserResponse(Guid UserId, string Email, string DisplayName, IReadOnlyCollection<string> Roles, bool IsSystemAdmin);
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, AuthenticatedUserResponse User);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken cancellationToken);
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}
