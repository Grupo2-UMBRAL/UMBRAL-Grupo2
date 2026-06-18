using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoringMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scoring_ops");

            migrationBuilder.CreateTable(
                name: "scoreboards",
                schema: "scoring_ops",
                columns: table => new
                {
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoreboards", x => x.live_session_id);
                });

            migrationBuilder.CreateTable(
                name: "score_entries",
                schema: "scoring_ops",
                columns: table => new
                {
                    score_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    accumulated_score_before = table.Column<int>(type: "integer", nullable: false),
                    accumulated_score_after = table.Column<int>(type: "integer", nullable: false),
                    visible_score_before = table.Column<int>(type: "integer", nullable: false),
                    visible_score_after = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mission_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penalty_command_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penalty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penalty_severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    resolution_time = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_entries", x => x.score_entry_id);
                    table.ForeignKey(
                        name: "FK_score_entries_scoreboards_live_session_id",
                        column: x => x.live_session_id,
                        principalSchema: "scoring_ops",
                        principalTable: "scoreboards",
                        principalColumn: "live_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_score_entries_live_session_id",
                schema: "scoring_ops",
                table: "score_entries",
                column: "live_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_entries_live_session_id_session_team_id",
                schema: "scoring_ops",
                table: "score_entries",
                columns: new[] { "live_session_id", "session_team_id" });

            migrationBuilder.CreateIndex(
                name: "ux_score_entries_single_stage_credit",
                schema: "scoring_ops",
                table: "score_entries",
                columns: new[] { "live_session_id", "session_team_id", "mission_stage_id" },
                unique: true,
                filter: "mission_stage_id IS NOT NULL AND entry_type IN ('StageCredit', 'ValidationOverrideCredit')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_entries",
                schema: "scoring_ops");

            migrationBuilder.DropTable(
                name: "scoreboards",
                schema: "scoring_ops");
        }
    }
}
