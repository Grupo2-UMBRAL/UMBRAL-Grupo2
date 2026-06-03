using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbral.SessionOperations.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleasedHintsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "released_hints",
                schema: "session_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    HintId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UnlockReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_released_hints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_released_hints_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_operations",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_released_hints_session_teams_SessionTeamId",
                        column: x => x.SessionTeamId,
                        principalSchema: "session_operations",
                        principalTable: "session_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_released_hints_live_session_team",
                schema: "session_operations",
                table: "released_hints",
                columns: new[] { "LiveSessionId", "SessionTeamId" });

            migrationBuilder.CreateIndex(
                name: "ix_released_hints_session_team_hint",
                schema: "session_operations",
                table: "released_hints",
                columns: new[] { "LiveSessionId", "SessionTeamId", "MissionStageId", "HintId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_released_hints_SessionTeamId",
                schema: "session_operations",
                table: "released_hints",
                column: "SessionTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "released_hints",
                schema: "session_operations");
        }
    }
}
