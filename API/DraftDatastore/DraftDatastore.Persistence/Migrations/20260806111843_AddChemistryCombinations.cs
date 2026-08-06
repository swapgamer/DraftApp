using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftDatastore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChemistryCombinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChemistryCombinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemistryCombinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChemistryCombinationPlayers",
                columns: table => new
                {
                    ChemistryCombinationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemistryCombinationPlayers", x => new { x.ChemistryCombinationId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ChemistryCombinationPlayers_ChemistryCombinations_ChemistryCombinationId",
                        column: x => x.ChemistryCombinationId,
                        principalTable: "ChemistryCombinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChemistryCombinationPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChemistryCombinationPlayers_PlayerId_ChemistryCombinationId",
                table: "ChemistryCombinationPlayers",
                columns: new[] { "PlayerId", "ChemistryCombinationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChemistryCombinations_IsDeleted_Type",
                table: "ChemistryCombinations",
                columns: new[] { "IsDeleted", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChemistryCombinationPlayers");

            migrationBuilder.DropTable(
                name: "ChemistryCombinations");
        }
    }
}
