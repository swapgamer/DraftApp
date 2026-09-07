using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepresentativeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionTeams", x => x.Id);
                    table.CheckConstraint("CK_AuctionTeams_Balance", "[StartingBalance] >= 0");
                    table.ForeignKey(
                        name: "FK_AuctionTeams_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionTeams_Users_RepresentativeUserId",
                        column: x => x.RepresentativeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoldPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionAssignments", x => x.Id);
                    table.CheckConstraint("CK_AuctionAssignments_SoldPrice", "[SoldPrice] > 0");
                    table.ForeignKey(
                        name: "FK_AuctionAssignments_AuctionTeams_AuctionTeamId",
                        column: x => x.AuctionTeamId,
                        principalTable: "AuctionTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionAssignments_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionAssignments_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionTeamMembers",
                columns: table => new
                {
                    AuctionTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionTeamMembers", x => new { x.AuctionTeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AuctionTeamMembers_AuctionTeams_AuctionTeamId",
                        column: x => x.AuctionTeamId,
                        principalTable: "AuctionTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionTeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionAssignments_AuctionId_PlayerId",
                table: "AuctionAssignments",
                columns: new[] { "AuctionId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionAssignments_AuctionTeamId_CreatedAtUtc",
                table: "AuctionAssignments",
                columns: new[] { "AuctionTeamId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionAssignments_PlayerId",
                table: "AuctionAssignments",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_IsActive",
                table: "Auctions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTeamMembers_UserId",
                table: "AuctionTeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTeams_AuctionId_Status",
                table: "AuctionTeams",
                columns: new[] { "AuctionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTeams_AuctionId_TeamName",
                table: "AuctionTeams",
                columns: new[] { "AuctionId", "TeamName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTeams_RepresentativeUserId",
                table: "AuctionTeams",
                column: "RepresentativeUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionAssignments");

            migrationBuilder.DropTable(
                name: "AuctionTeamMembers");

            migrationBuilder.DropTable(
                name: "AuctionTeams");

            migrationBuilder.DropTable(
                name: "Auctions");
        }
    }
}
