using DraftDatastore.Domain.Common;

namespace DraftDatastore.Domain.Entities;

public sealed class User : AuditableEntity, ISoftDeletable
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSystemAdmin { get; set; }
    public DateTimeOffset? AdminExpiresAtUtc { get; set; }
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
    public int? OverallRank { get; set; }
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

// Auction data deliberately references the existing player catalogue rather than copying it.
// A player can therefore keep one canonical profile while being assigned once per auction.
public sealed class Auction : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<AuctionTeam> Teams { get; set; } = new List<AuctionTeam>();
    public ICollection<AuctionAssignment> Assignments { get; set; } = new List<AuctionAssignment>();
    public ICollection<AuctionLot> Lots { get; set; } = new List<AuctionLot>();
    public ICollection<AuctionLiveSeat> LiveSeats { get; set; } = new List<AuctionLiveSeat>();
}

public sealed class AuctionTeam : AuditableEntity
{
    public Guid AuctionId { get; set; }
    public Guid RepresentativeUserId { get; set; }
    // The original representative owns the team request. A live bidder is selected
    // separately so an approved team can nominate any one of its members per session.
    public Guid? LiveBidderUserId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public decimal StartingBalance { get; set; }
    public string Status { get; set; } = AuctionTeamStatuses.Pending;
    public Auction Auction { get; set; } = null!;
    public User RepresentativeUser { get; set; } = null!;
    public User? LiveBidderUser { get; set; }
    public ICollection<AuctionTeamMember> Members { get; set; } = new List<AuctionTeamMember>();
    public ICollection<AuctionAssignment> Assignments { get; set; } = new List<AuctionAssignment>();
    public ICollection<AuctionBid> Bids { get; set; } = new List<AuctionBid>();
    public ICollection<AuctionLiveSeat> LiveSeats { get; set; } = new List<AuctionLiveSeat>();
}

public sealed class AuctionTeamMember
{
    public Guid AuctionTeamId { get; set; }
    public Guid UserId { get; set; }
    public AuctionTeam AuctionTeam { get; set; } = null!;
    public User User { get; set; } = null!;
}

public sealed class AuctionAssignment : AuditableEntity
{
    public Guid AuctionId { get; set; }
    public Guid AuctionTeamId { get; set; }
    public Guid PlayerId { get; set; }
    public decimal SoldPrice { get; set; }
    public Auction Auction { get; set; } = null!;
    public AuctionTeam AuctionTeam { get; set; } = null!;
    public Player Player { get; set; } = null!;
}

// Live auction lots are separate from manual AuctionAssignments. This lets the
// existing board remain operational while a bid is being collected and settled.
public sealed class AuctionLot : AuditableEntity
{
    public Guid AuctionId { get; set; }
    public Guid PlayerId { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal? CurrentBidAmount { get; set; }
    public Guid? HighestBidAuctionTeamId { get; set; }
    public string State { get; set; } = AuctionLotStates.Draft;
    public DateTimeOffset? EndsAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int ExtensionCount { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Auction Auction { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public AuctionTeam? HighestBidAuctionTeam { get; set; }
    public ICollection<AuctionBid> Bids { get; set; } = new List<AuctionBid>();
}

public sealed class AuctionBid : AuditableEntity
{
    public Guid AuctionLotId { get; set; }
    public Guid AuctionTeamId { get; set; }
    public Guid BidderUserId { get; set; }
    public decimal Amount { get; set; }
    public AuctionLot AuctionLot { get; set; } = null!;
    public AuctionTeam AuctionTeam { get; set; } = null!;
    public User BidderUser { get; set; } = null!;
}

// A short-lived persisted seat makes the free-tier connection limits explicit
// and allows the Live Hub to recover after an App Service restart.
public sealed class AuctionLiveSeat : AuditableEntity
{
    public Guid AuctionId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AuctionTeamId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string SeatKind { get; set; } = AuctionSeatKinds.Viewer;
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public Auction Auction { get; set; } = null!;
    public User User { get; set; } = null!;
    public AuctionTeam? AuctionTeam { get; set; }
}

public static class AuctionTeamStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class AuctionLotStates
{
    public const string Draft = "Draft";
    public const string Open = "Open";
    public const string Paused = "Paused";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

public static class AuctionSeatKinds
{
    public const string Admin = "Admin";
    public const string Bidder = "Bidder";
    public const string Viewer = "Viewer";
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
