using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RocketLeagueStats.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialEventStoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FirstSeenAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    InitializedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    EndedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    DestroyedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    WinnerTeamNum = table.Column<int>(type: "INTEGER", nullable: true),
                    EventCount = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotCount = table.Column<long>(type: "INTEGER", nullable: false),
                    LastEventAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchGuid);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Matches_MatchGuid",
                        column: x => x.MatchGuid,
                        principalTable: "Matches",
                        principalColumn: "MatchGuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchSnapshots_Matches_MatchGuid",
                        column: x => x.MatchGuid,
                        principalTable: "Matches",
                        principalColumn: "MatchGuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventParticipants",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MatchGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Shortcut = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamNum = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipants", x => new { x.EventId, x.PlayerName, x.Role });
                    table.ForeignKey(
                        name: "FK_EventParticipants_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_MatchGuid_PlayerName",
                table: "EventParticipants",
                columns: new[] { "MatchGuid", "PlayerName" });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_PlayerName_TimestampUtc",
                table: "EventParticipants",
                columns: new[] { "PlayerName", "TimestampUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventName_TimestampUtc",
                table: "Events",
                columns: new[] { "EventName", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_MatchGuid_Id",
                table: "Events",
                columns: new[] { "MatchGuid", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_EndedAtUtc",
                table: "Matches",
                column: "EndedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MatchSnapshots_MatchGuid_Id",
                table: "MatchSnapshots",
                columns: new[] { "MatchGuid", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventParticipants");

            migrationBuilder.DropTable(
                name: "MatchSnapshots");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
