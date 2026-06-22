using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceSubmissionsAndTeamProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "sequence_number",
                schema: "session_management",
                table: "live_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "evidence_submissions",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmittedHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_submissions_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_management",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evidence_submissions_session_teams_SessionTeamId",
                        column: x => x.SessionTeamId,
                        principalSchema: "session_management",
                        principalTable: "session_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_team_progressions",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStageIndex = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_team_progressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_team_progressions_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_management",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_team_progressions_session_teams_SessionTeamId",
                        column: x => x.SessionTeamId,
                        principalSchema: "session_management",
                        principalTable: "session_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_submissions_live_session_team_stage",
                schema: "session_management",
                table: "evidence_submissions",
                columns: new[] { "LiveSessionId", "SessionTeamId", "MissionStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_submissions_SessionTeamId",
                schema: "session_management",
                table: "evidence_submissions",
                column: "SessionTeamId");

            migrationBuilder.CreateIndex(
                name: "ix_session_team_progressions_live_session_id_session_team_id",
                schema: "session_management",
                table: "session_team_progressions",
                columns: new[] { "LiveSessionId", "SessionTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_team_progressions_SessionTeamId",
                schema: "session_management",
                table: "session_team_progressions",
                column: "SessionTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_submissions",
                schema: "session_management");

            migrationBuilder.DropTable(
                name: "session_team_progressions",
                schema: "session_management");

            migrationBuilder.DropColumn(
                name: "sequence_number",
                schema: "session_management",
                table: "live_sessions");
        }
    }
}
