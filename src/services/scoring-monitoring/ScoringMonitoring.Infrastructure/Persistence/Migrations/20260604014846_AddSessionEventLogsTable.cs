using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoringMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEventLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_event_logs",
                schema: "scoring_ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_event_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_session_event_logs_live_session_id_timestamp",
                schema: "scoring_ops",
                table: "session_event_logs",
                columns: new[] { "live_session_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_event_logs",
                schema: "scoring_ops");
        }
    }
}
