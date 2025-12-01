using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongStats.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", nullable: false),
                    Players = table.Column<string>(type: "TEXT", nullable: false),
                    PointsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FetchedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", nullable: false),
                    RoundJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredRounds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredGames_CreatedDateTime",
                table: "StoredGames",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_StoredGames_GameId",
                table: "StoredGames",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredRounds_GameId",
                table: "StoredRounds",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredRounds");

            migrationBuilder.DropTable(
                name: "StoredGames");
        }
    }
}
