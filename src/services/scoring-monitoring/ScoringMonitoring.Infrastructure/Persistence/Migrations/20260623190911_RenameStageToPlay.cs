using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoringMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameStageToPlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_score_entries_single_stage_credit",
                schema: "scoring_monitoring",
                table: "score_entries");

            migrationBuilder.RenameColumn(
                name: "mission_stage_id",
                schema: "scoring_monitoring",
                table: "score_entries",
                newName: "play_id");

            migrationBuilder.CreateIndex(
                name: "ux_score_entries_single_play_credit",
                schema: "scoring_monitoring",
                table: "score_entries",
                columns: new[] { "live_session_id", "session_team_id", "play_id" },
                unique: true,
                filter: "play_id IS NOT NULL AND entry_type IN ('PlayCredit', 'ValidationOverrideCredit')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_score_entries_single_play_credit",
                schema: "scoring_monitoring",
                table: "score_entries");

            migrationBuilder.RenameColumn(
                name: "play_id",
                schema: "scoring_monitoring",
                table: "score_entries",
                newName: "mission_stage_id");

            migrationBuilder.CreateIndex(
                name: "ux_score_entries_single_stage_credit",
                schema: "scoring_monitoring",
                table: "score_entries",
                columns: new[] { "live_session_id", "session_team_id", "mission_stage_id" },
                unique: true,
                filter: "mission_stage_id IS NOT NULL AND entry_type IN ('StageCredit', 'ValidationOverrideCredit')");
        }
    }
}
