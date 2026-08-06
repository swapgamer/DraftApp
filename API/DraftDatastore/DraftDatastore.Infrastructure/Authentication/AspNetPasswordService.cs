using DraftDatastore.Application.Authentication;
using DraftDatastore.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DraftDatastore.Infrastructure.Authentication;

public sealed class AspNetPasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    public string Hash(User user, string password) => _hasher.HashPassword(user, password);
    public bool Verify(User user, string passwordHash, string suppliedPassword) => _hasher.VerifyHashedPassword(user, passwordHash, suppliedPassword) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
