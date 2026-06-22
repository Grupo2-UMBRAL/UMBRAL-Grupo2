using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260603153000_AddTriviaSubmissionsAndValidationOverrides")]
    public partial class AddTriviaSubmissionsAndValidationOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SubmittedHash",
                schema: "session_management",
                table: "evidence_submissions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedText",
                schema: "session_management",
                table: "evidence_submissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "validation_override_logs",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviousOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OverriddenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_override_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_validation_override_logs_evidence_submissions_EvidenceSubmiss~",
                        column: x => x.EvidenceSubmissionId,
                        principalSchema: "session_management",
                        principalTable: "evidence_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_validation_override_logs_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_management",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_validation_override_logs_session_teams_SessionTeamId",
                        column: x => x.SessionTeamId,
                        principalSchema: "session_management",
                        principalTable: "session_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_validation_override_logs_evidence_submission_id",
                schema: "session_management",
                table: "validation_override_logs",
                column: "EvidenceSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_validation_override_logs_LiveSessionId",
                schema: "session_management",
                table: "validation_override_logs",
                column: "LiveSessionId");

            migrationBuilder.CreateIndex(
                name: "ix_validation_override_logs_live_session_team_stage",
                schema: "session_management",
                table: "validation_override_logs",
                columns: new[] { "LiveSessionId", "SessionTeamId", "MissionStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_validation_override_logs_SessionTeamId",
                schema: "session_management",
                table: "validation_override_logs",
                column: "SessionTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "validation_override_logs",
                schema: "session_management");

            migrationBuilder.DropColumn(
                name: "SubmittedText",
                schema: "session_management",
                table: "evidence_submissions");

            migrationBuilder.AlterColumn<string>(
                name: "SubmittedHash",
                schema: "session_management",
                table: "evidence_submissions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
