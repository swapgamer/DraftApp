using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdminExpiresAtUtc",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsSystemAdmin_AdminExpiresAtUtc",
                table: "Users",
                columns: new[] { "IsSystemAdmin", "AdminExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsSystemAdmin_AdminExpiresAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AdminExpiresAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsSystemAdmin",
                table: "Users");

        }
    }
}
