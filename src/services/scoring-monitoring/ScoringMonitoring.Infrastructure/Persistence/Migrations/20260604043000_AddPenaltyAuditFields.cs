using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoringMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "applied_by_operator_user_id",
                schema: "scoring_monitoring",
                table: "score_entries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "penalty_reason",
                schema: "scoring_monitoring",
                table: "score_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_score_entries_penalty_command",
                schema: "scoring_monitoring",
                table: "score_entries",
                columns: new[] { "live_session_id", "penalty_command_id" },
                unique: true,
                filter: "penalty_command_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_score_entries_penalty_command",
                schema: "scoring_monitoring",
                table: "score_entries");

            migrationBuilder.DropColumn(
                name: "applied_by_operator_user_id",
                schema: "scoring_monitoring",
                table: "score_entries");

            migrationBuilder.DropColumn(
                name: "penalty_reason",
                schema: "scoring_monitoring",
                table: "score_entries");
        }
    }
}
