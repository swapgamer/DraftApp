using DraftDatastore.Domain.Common;
using DraftDatastore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DraftDatastore.Persistence;

public sealed class DraftDatastoreDbContext(DbContextOptions<DraftDatastoreDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerAlias> PlayerAliases => Set<PlayerAlias>();
    public DbSet<PlayerPosition> PlayerPositions => Set<PlayerPosition>();
    public DbSet<PlayerImage> PlayerImages => Set<PlayerImage>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<PlayingEra> PlayingEras => Set<PlayingEra>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ChatHistory> ChatHistories => Set<ChatHistory>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<ChemistryCombination> ChemistryCombinations => Set<ChemistryCombination>();
    public DbSet<ChemistryCombinationPlayer> ChemistryCombinationPlayers => Set<ChemistryCombinationPlayer>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<AuctionTeam> AuctionTeams => Set<AuctionTeam>();
    public DbSet<AuctionTeamMember> AuctionTeamMembers => Set<AuctionTeamMember>();
    public DbSet<AuctionAssignment> AuctionAssignments => Set<AuctionAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureCatalogue(modelBuilder);
        ConfigureOperationalData(modelBuilder);
        SeedReferenceData(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditingAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditingAndSoftDelete()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAtUtc = now;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAtUtc = now;
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>().Where(x => x.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = now;
        }
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => new { x.IsSystemAdmin, x.AdminExpiresAtUtc });
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
        builder.Entity<Role>(entity => { entity.ToTable("Roles"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(64).IsRequired(); entity.HasIndex(x => x.Name).IsUnique(); });
        builder.Entity<UserRole>(entity => { entity.ToTable("UserRoles"); entity.HasKey(x => new { x.UserId, x.RoleId }); entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId); entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId); });
        builder.Entity<RefreshToken>(entity => { entity.ToTable("RefreshTokens"); entity.HasKey(x => x.Id); entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); entity.HasIndex(x => x.TokenHash).IsUnique(); entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc }); entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
    }

    private static void ConfigureCatalogue(ModelBuilder builder)
    {
        builder.Entity<Nationality>(entity => { entity.ToTable("Nationalities"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(100).IsRequired(); entity.Property(x => x.IsoCode).HasMaxLength(3).IsRequired(); entity.HasIndex(x => x.IsoCode).IsUnique(); });
        builder.Entity<PlayingEra>(entity => { entity.ToTable("PlayingEras", table => table.HasCheckConstraint("CK_PlayingEras_Years", "[StartYear] <= [EndYear]")); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(80).IsRequired(); entity.HasIndex(x => x.Name).IsUnique(); });
        builder.Entity<Position>(entity => { entity.ToTable("Positions"); entity.HasKey(x => x.Id); entity.Property(x => x.Code).HasMaxLength(8).IsRequired(); entity.Property(x => x.Name).HasMaxLength(60).IsRequired(); entity.HasIndex(x => x.Code).IsUnique(); });
        builder.Entity<Player>(entity =>
        {
            entity.ToTable("Players", table => { table.HasCheckConstraint("CK_Players_Rank", "[OverallRank] > 0"); table.HasCheckConstraint("CK_Players_Points", "[GoalCreditPoints] >= 0 AND [AssistCreditPoints] >= 0 AND [DefensiveCreditPoints] >= 0"); });
            entity.HasKey(x => x.Id); entity.Property(x => x.FullName).HasMaxLength(160).IsRequired(); entity.Property(x => x.ShortDescription).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.GoalCreditPoints).HasPrecision(10, 2); entity.Property(x => x.AssistCreditPoints).HasPrecision(10, 2); entity.Property(x => x.DefensiveCreditPoints).HasPrecision(10, 2);
            entity.Property(x => x.TransfermarktUrl).HasMaxLength(2048); entity.Property(x => x.WikipediaUrl).HasMaxLength(2048);
            entity.HasIndex(x => new { x.IsDeleted, x.OverallRank }); entity.HasIndex(x => new { x.NationalityId, x.IsDeleted }); entity.HasIndex(x => new { x.IsDeleted, x.GoalCreditPoints }); entity.HasIndex(x => new { x.IsDeleted, x.DefensiveCreditPoints });
            entity.HasOne(x => x.Nationality).WithMany(x => x.Players).HasForeignKey(x => x.NationalityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PlayingEra).WithMany(x => x.Players).HasForeignKey(x => x.PlayingEraId).OnDelete(DeleteBehavior.Restrict); entity.HasQueryFilter(x => !x.IsDeleted);
        });
        builder.Entity<PlayerAlias>(entity => { entity.ToTable("PlayerAliases"); entity.HasKey(x => x.Id); entity.Property(x => x.Alias).HasMaxLength(160).IsRequired(); entity.HasIndex(x => new { x.PlayerId, x.Alias }).IsUnique(); entity.HasOne(x => x.Player).WithMany(x => x.Aliases).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<PlayerPosition>(entity => { entity.ToTable("PlayerPositions", table => table.HasCheckConstraint("CK_PlayerPositions_Rank", "[OverallRank] IS NULL OR [OverallRank] > 0")); entity.HasKey(x => new { x.PlayerId, x.PositionId }); entity.HasIndex(x => new { x.PositionId, x.OverallRank, x.PlayerId }); entity.HasOne(x => x.Player).WithMany(x => x.PlayerPositions).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.Position).WithMany(x => x.PlayerPositions).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<PlayerImage>(entity => { entity.ToTable("PlayerImages"); entity.HasKey(x => x.Id); entity.Property(x => x.BlobPath).HasMaxLength(1024).IsRequired(); entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired(); entity.HasIndex(x => x.BlobPath).IsUnique(); entity.HasOne(x => x.Player).WithMany(x => x.Images).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<Favorite>(entity => { entity.ToTable("Favorites"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.UserId, x.PlayerId }).IsUnique(); entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<ChemistryCombination>(entity => { entity.ToTable("ChemistryCombinations"); entity.HasKey(x => x.Id); entity.Property(x => x.Type).HasMaxLength(8).IsRequired(); entity.Property(x => x.Title).HasMaxLength(160); entity.HasIndex(x => new { x.IsDeleted, x.Type }); entity.HasQueryFilter(x => !x.IsDeleted); });
        builder.Entity<ChemistryCombinationPlayer>(entity => { entity.ToTable("ChemistryCombinationPlayers"); entity.HasKey(x => new { x.ChemistryCombinationId, x.PlayerId }); entity.HasIndex(x => new { x.PlayerId, x.ChemistryCombinationId }); entity.HasOne(x => x.ChemistryCombination).WithMany(x => x.Players).HasForeignKey(x => x.ChemistryCombinationId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.Player).WithMany(x => x.ChemistryCombinations).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<Auction>(entity => { entity.ToTable("Auctions"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(120).IsRequired(); entity.HasIndex(x => x.IsActive).IsUnique().HasFilter("[IsActive] = 1"); });
        builder.Entity<AuctionTeam>(entity => { entity.ToTable("AuctionTeams", table => table.HasCheckConstraint("CK_AuctionTeams_Balance", "[StartingBalance] >= 0")); entity.HasKey(x => x.Id); entity.Property(x => x.TeamName).HasMaxLength(120).IsRequired(); entity.Property(x => x.Icon).HasMaxLength(512); entity.Property(x => x.Status).HasMaxLength(16).IsRequired(); entity.Property(x => x.StartingBalance).HasPrecision(18, 2); entity.HasIndex(x => new { x.AuctionId, x.TeamName }).IsUnique(); entity.HasIndex(x => new { x.AuctionId, x.Status }); entity.HasOne(x => x.Auction).WithMany(x => x.Teams).HasForeignKey(x => x.AuctionId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.RepresentativeUser).WithMany().HasForeignKey(x => x.RepresentativeUserId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<AuctionTeamMember>(entity => { entity.ToTable("AuctionTeamMembers"); entity.HasKey(x => new { x.AuctionTeamId, x.UserId }); entity.HasIndex(x => x.UserId); entity.HasOne(x => x.AuctionTeam).WithMany(x => x.Members).HasForeignKey(x => x.AuctionTeamId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<AuctionAssignment>(entity => { entity.ToTable("AuctionAssignments", table => table.HasCheckConstraint("CK_AuctionAssignments_SoldPrice", "[SoldPrice] > 0")); entity.HasKey(x => x.Id); entity.Property(x => x.SoldPrice).HasPrecision(18, 2); entity.HasIndex(x => new { x.AuctionId, x.PlayerId }).IsUnique(); entity.HasIndex(x => new { x.AuctionTeamId, x.CreatedAtUtc }); entity.HasOne(x => x.Auction).WithMany(x => x.Assignments).HasForeignKey(x => x.AuctionId).OnDelete(DeleteBehavior.Restrict); entity.HasOne(x => x.AuctionTeam).WithMany(x => x.Assignments).HasForeignKey(x => x.AuctionTeamId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureOperationalData(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(entity => { entity.ToTable("AuditLogs"); entity.HasKey(x => x.Id); entity.Property(x => x.Action).HasMaxLength(80).IsRequired(); entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired(); entity.Property(x => x.EntityId).HasMaxLength(64).IsRequired(); entity.Property(x => x.ChangesJson).HasColumnType("nvarchar(max)"); entity.HasIndex(x => new { x.EntityName, x.EntityId, x.OccurredAtUtc }); });
        builder.Entity<LoginHistory>(entity => { entity.ToTable("LoginHistories"); entity.HasKey(x => x.Id); entity.Property(x => x.IpAddress).HasMaxLength(64); entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc }); entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<ChatHistory>(entity => { entity.ToTable("ChatHistories"); entity.HasKey(x => x.Id); entity.Property(x => x.Message).HasMaxLength(1000).IsRequired(); entity.Property(x => x.Intent).HasMaxLength(64).IsRequired(); entity.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)").IsRequired(); entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
    }

    private static void SeedReferenceData(ModelBuilder builder)
    {
        builder.Entity<Role>().HasData(new Role { Id = 1, Name = SystemRoles.User }, new Role { Id = 2, Name = SystemRoles.Admin });
        builder.Entity<Position>().HasData(new Position { Id = 1, Code = "GK", Name = "Goalkeeper" }, new Position { Id = 2, Code = "CB", Name = "Centre Back" }, new Position { Id = 3, Code = "CM", Name = "Central Midfielder" }, new Position { Id = 4, Code = "ST", Name = "Striker" });
        builder.Entity<Nationality>().HasData(new Nationality { Id = 1, Name = "Italy", IsoCode = "ITA" }, new Nationality { Id = 2, Name = "Argentina", IsoCode = "ARG" }, new Nationality { Id = 3, Name = "Portugal", IsoCode = "PRT" });
        builder.Entity<PlayingEra>().HasData(new PlayingEra { Id = 1, Name = "Classic", StartYear = 1900, EndYear = 1969 }, new PlayingEra { Id = 2, Name = "Modern", StartYear = 1970, EndYear = 2026 });
    }
}



