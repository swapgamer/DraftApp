using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLiveAuctionHubPhase2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuctionLiveSeats",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                SeatKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuctionLiveSeats", x => x.Id);
                table.ForeignKey("FK_AuctionLiveSeats_Auctions_AuctionId", x => x.AuctionId, "Auctions", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_AuctionLiveSeats_AuctionTeams_AuctionTeamId", x => x.AuctionTeamId, "AuctionTeams", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_AuctionLiveSeats_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuctionLots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CurrentBidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                HighestBidAuctionTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ExtensionCount = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuctionLots", x => x.Id);
                table.CheckConstraint("CK_AuctionLots_StartingPrice", "[StartingPrice] > 0");
                table.CheckConstraint("CK_AuctionLots_CurrentBid", "[CurrentBidAmount] IS NULL OR [CurrentBidAmount] > 0");
                table.CheckConstraint("CK_AuctionLots_ExtensionCount", "[ExtensionCount] >= 0");
                table.ForeignKey("FK_AuctionLots_Auctions_AuctionId", x => x.AuctionId, "Auctions", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_AuctionLots_AuctionTeams_HighestBidAuctionTeamId", x => x.HighestBidAuctionTeamId, "AuctionTeams", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AuctionLots_Players_PlayerId", x => x.PlayerId, "Players", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuctionBids",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BidderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuctionBids", x => x.Id);
                table.CheckConstraint("CK_AuctionBids_Amount", "[Amount] > 0");
                table.ForeignKey("FK_AuctionBids_AuctionLots_AuctionLotId", x => x.AuctionLotId, "AuctionLots", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_AuctionBids_AuctionTeams_AuctionTeamId", x => x.AuctionTeamId, "AuctionTeams", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AuctionBids_Users_BidderUserId", x => x.BidderUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_AuctionBids_AuctionLotId_CreatedAtUtc", "AuctionBids", new[] { "AuctionLotId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_AuctionBids_AuctionTeamId_CreatedAtUtc", "AuctionBids", new[] { "AuctionTeamId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_AuctionBids_BidderUserId", "AuctionBids", "BidderUserId");
        migrationBuilder.CreateIndex("IX_AuctionLiveSeats_AuctionId_ConnectionId", "AuctionLiveSeats", new[] { "AuctionId", "ConnectionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_AuctionLiveSeats_AuctionId_SeatKind_LastSeenAtUtc", "AuctionLiveSeats", new[] { "AuctionId", "SeatKind", "LastSeenAtUtc" });
        migrationBuilder.CreateIndex("IX_AuctionLiveSeats_AuctionId_UserId", "AuctionLiveSeats", new[] { "AuctionId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_AuctionLiveSeats_AuctionTeamId", "AuctionLiveSeats", "AuctionTeamId");
        migrationBuilder.CreateIndex("IX_AuctionLiveSeats_UserId", "AuctionLiveSeats", "UserId");
        migrationBuilder.CreateIndex("IX_AuctionLots_AuctionId_PlayerId", "AuctionLots", new[] { "AuctionId", "PlayerId" }, unique: true);
        migrationBuilder.CreateIndex("IX_AuctionLots_AuctionId_State_EndsAtUtc", "AuctionLots", new[] { "AuctionId", "State", "EndsAtUtc" });
        migrationBuilder.CreateIndex("IX_AuctionLots_HighestBidAuctionTeamId", "AuctionLots", "HighestBidAuctionTeamId");
        migrationBuilder.CreateIndex("IX_AuctionLots_PlayerId", "AuctionLots", "PlayerId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuctionBids");
        migrationBuilder.DropTable(name: "AuctionLiveSeats");
        migrationBuilder.DropTable(name: "AuctionLots");
    }
}
