using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionLotPausedRemainingSecondsColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PausedRemainingSeconds",
                table: "AuctionLots",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuctionLots_PausedRemainingSeconds",
                table: "AuctionLots",
                sql: "[PausedRemainingSeconds] IS NULL OR [PausedRemainingSeconds] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuctionLots_PausedRemainingSeconds",
                table: "AuctionLots");

            migrationBuilder.DropColumn(
                name: "PausedRemainingSeconds",
                table: "AuctionLots");
        }
    }
}
