using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerPositionRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerPositions_PositionId_PlayerId",
                table: "PlayerPositions");

            migrationBuilder.AddColumn<int>(
                name: "OverallRank",
                table: "PlayerPositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerPositions_Rank",
                table: "PlayerPositions",
                sql: "[OverallRank] IS NULL OR [OverallRank] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPositions_PositionId_OverallRank_PlayerId",
                table: "PlayerPositions",
                columns: new[] { "PositionId", "OverallRank", "PlayerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerPositions_Rank",
                table: "PlayerPositions");

            migrationBuilder.DropIndex(
                name: "IX_PlayerPositions_PositionId_OverallRank_PlayerId",
                table: "PlayerPositions");

            migrationBuilder.DropColumn(
                name: "OverallRank",
                table: "PlayerPositions");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPositions_PositionId_PlayerId",
                table: "PlayerPositions",
                columns: new[] { "PositionId", "PlayerId" });
        }
    }
}
