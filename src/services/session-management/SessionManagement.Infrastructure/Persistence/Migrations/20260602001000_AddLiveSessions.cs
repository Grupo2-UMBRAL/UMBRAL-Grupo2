using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SessionManagement.Infrastructure.Persistence;

#nullable disable

namespace SessionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SessionManagementDbContext))]
    [Migration("20260602001000_AddLiveSessions")]
    public partial class AddLiveSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_sessions",
                schema: "session_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScheduledStartAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SessionStageFlowJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_sessions_CreatedAtUtc",
                schema: "session_management",
                table: "live_sessions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_live_sessions_MissionId",
                schema: "session_management",
                table: "live_sessions",
                column: "MissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_sessions",
                schema: "session_management");
        }
    }
}
