using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<string>(type: "text", nullable: false),
                    Players = table.Column<string>(type: "text", nullable: false),
                    PointsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<string>(type: "text", nullable: false),
                    RoundJson = table.Column<string>(type: "text", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "StoredGames");

            migrationBuilder.DropTable(
                name: "StoredRounds");
        }
    }
}
