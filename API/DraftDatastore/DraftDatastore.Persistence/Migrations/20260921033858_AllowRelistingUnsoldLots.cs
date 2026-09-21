using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowRelistingUnsoldLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuctionLots_AuctionId_PlayerId",
                table: "AuctionLots");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionLots_AuctionId_PlayerId",
                table: "AuctionLots",
                columns: new[] { "AuctionId", "PlayerId" },
                unique: true,
                filter: "[State] <> 'Closed' AND [State] <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuctionLots_AuctionId_PlayerId",
                table: "AuctionLots");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionLots_AuctionId_PlayerId",
                table: "AuctionLots",
                columns: new[] { "AuctionId", "PlayerId" },
                unique: true);
        }
    }
}
