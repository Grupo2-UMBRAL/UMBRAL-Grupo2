using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SessionManagement.Infrastructure.Persistence;

#nullable disable

namespace SessionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SessionManagementDbContext))]
    [Migration("20260602153000_AddSessionEnrollment")]
    public partial class AddSessionEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "join_code_value",
                schema: "session_management",
                table: "live_sessions",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enrollment_window_opened_at_utc",
                schema: "session_management",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enrollment_window_closed_at_utc",
                schema: "session_management",
                table: "live_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_teams",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_teams_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_management",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_participations",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EnrolledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_participations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_participations_live_sessions_LiveSessionId",
                        column: x => x.LiveSessionId,
                        principalSchema: "session_management",
                        principalTable: "live_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_participations_session_teams_SessionTeamId",
                        column: x => x.SessionTeamId,
                        principalSchema: "session_management",
                        principalTable: "session_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_sessions_join_code_value",
                schema: "session_management",
                table: "live_sessions",
                column: "join_code_value",
                unique: true,
                filter: "join_code_value IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_teams_live_session_id_normalized_name",
                schema: "session_management",
                table: "session_teams",
                columns: new[] { "LiveSessionId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_participations_live_session_id_participant_user_id",
                schema: "session_management",
                table: "team_participations",
                columns: new[] { "LiveSessionId", "ParticipantUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_participations_SessionTeamId",
                schema: "session_management",
                table: "team_participations",
                column: "SessionTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_participations",
                schema: "session_management");

            migrationBuilder.DropTable(
                name: "session_teams",
                schema: "session_management");

            migrationBuilder.DropIndex(
                name: "ix_live_sessions_join_code_value",
                schema: "session_management",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "join_code_value",
                schema: "session_management",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "enrollment_window_opened_at_utc",
                schema: "session_management",
                table: "live_sessions");

            migrationBuilder.DropColumn(
                name: "enrollment_window_closed_at_utc",
                schema: "session_management",
                table: "live_sessions");
        }
    }
}
