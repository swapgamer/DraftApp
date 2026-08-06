using DraftDatastore.Domain.Common;

namespace DraftDatastore.Domain.Entities;

public sealed class User : AuditableEntity, ISoftDeletable
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIpAddress { get; set; }
    public User User { get; set; } = null!;
}

public sealed class Nationality
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IsoCode { get; set; } = string.Empty;
    public ICollection<Player> Players { get; set; } = new List<Player>();
}

public sealed class PlayingEra
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public short StartYear { get; set; }
    public short EndYear { get; set; }
    public ICollection<Player> Players { get; set; } = new List<Player>();
}

public sealed class Position
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<PlayerPosition> PlayerPositions { get; set; } = new List<PlayerPosition>();
}

public sealed class Player : AuditableEntity, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public int NationalityId { get; set; }
    public int PlayingEraId { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public int OverallRank { get; set; }
    public decimal GoalCreditPoints { get; set; }
    public decimal AssistCreditPoints { get; set; }
    public decimal DefensiveCreditPoints { get; set; }
    public string? TransfermarktUrl { get; set; }
    public string? WikipediaUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Nationality Nationality { get; set; } = null!;
    public PlayingEra PlayingEra { get; set; } = null!;
    public ICollection<PlayerAlias> Aliases { get; set; } = new List<PlayerAlias>();
    public ICollection<PlayerPosition> PlayerPositions { get; set; } = new List<PlayerPosition>();
    public ICollection<PlayerImage> Images { get; set; } = new List<PlayerImage>();
    public ICollection<ChemistryCombinationPlayer> ChemistryCombinations { get; set; } = new List<ChemistryCombinationPlayer>();
}

public sealed class ChemistryCombination : AuditableEntity, ISoftDeletable
{
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public ICollection<ChemistryCombinationPlayer> Players { get; set; } = new List<ChemistryCombinationPlayer>();
}

public sealed class ChemistryCombinationPlayer
{
    public Guid ChemistryCombinationId { get; set; }
    public Guid PlayerId { get; set; }
    public int DisplayOrder { get; set; }
    public ChemistryCombination ChemistryCombination { get; set; } = null!;
    public Player Player { get; set; } = null!;
}

public sealed class PlayerAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public Player Player { get; set; } = null!;
}

public sealed class PlayerPosition
{
    public Guid PlayerId { get; set; }
    public int PositionId { get; set; }
    public bool IsPrimary { get; set; }
    public Player Player { get; set; } = null!;
    public Position Position { get; set; } = null!;
}

public sealed class PlayerImage : AuditableEntity
{
    public Guid PlayerId { get; set; }
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Player Player { get; set; } = null!;
}

public sealed class Favorite : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid PlayerId { get; set; }
    public User User { get; set; } = null!;
    public Player Player { get; set; } = null!;
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? ChangesJson { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class ChatHistory : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public bool HasMatch { get; set; }
    public long DurationMilliseconds { get; set; }
    public User User { get; set; } = null!;
}

public static class SystemRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
}
public static class SystemRoleIds
{
    public const int User = 1;
    public const int Admin = 2;
}
