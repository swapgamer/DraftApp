using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveAuctionBidderSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LiveBidderUserId",
                table: "AuctionTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTeams_LiveBidderUserId",
                table: "AuctionTeams",
                column: "LiveBidderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuctionTeams_Users_LiveBidderUserId",
                table: "AuctionTeams",
                column: "LiveBidderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionTeams_Users_LiveBidderUserId",
                table: "AuctionTeams");

            migrationBuilder.DropIndex(
                name: "IX_AuctionTeams_LiveBidderUserId",
                table: "AuctionTeams");

            migrationBuilder.DropColumn(
                name: "LiveBidderUserId",
                table: "AuctionTeams");

        }
    }
}
