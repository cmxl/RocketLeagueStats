using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RocketLeagueStats.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMetadataAndPlayerStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Arena",
                table: "Matches",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlueColorPrimary",
                table: "Matches",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlueColorSecondary",
                table: "Matches",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlueTeamName",
                table: "Matches",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrangeColorPrimary",
                table: "Matches",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrangeColorSecondary",
                table: "Matches",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrangeTeamName",
                table: "Matches",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerMatchStats",
                columns: table => new
                {
                    MatchGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Shortcut = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TeamNum = table.Column<int>(type: "INTEGER", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    Shots = table.Column<int>(type: "INTEGER", nullable: false),
                    Touches = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMatchStats", x => new { x.MatchGuid, x.Shortcut });
                    table.ForeignKey(
                        name: "FK_PlayerMatchStats_Matches_MatchGuid",
                        column: x => x.MatchGuid,
                        principalTable: "Matches",
                        principalColumn: "MatchGuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStats_PlayerName",
                table: "PlayerMatchStats",
                column: "PlayerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerMatchStats");

            migrationBuilder.DropColumn(
                name: "Arena",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BlueColorPrimary",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BlueColorSecondary",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BlueTeamName",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "OrangeColorPrimary",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "OrangeColorSecondary",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "OrangeTeamName",
                table: "Matches");
        }
    }
}
