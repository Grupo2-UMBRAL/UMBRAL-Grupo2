using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MissionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchemaBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mission_management");

            migrationBuilder.CreateTable(
                name: "missions",
                schema: "mission_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MaximumDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    GameType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NodeTreeJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mission_stages",
                schema: "mission_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GameType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpectedQrHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TriviaValidationCriteria = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mission_stages_missions_MissionId",
                        column: x => x.MissionId,
                        principalSchema: "mission_management",
                        principalTable: "missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_stage_hints",
                schema: "mission_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsSolution = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_stage_hints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mission_stage_hints_mission_stages_MissionStageId",
                        column: x => x.MissionStageId,
                        principalSchema: "mission_management",
                        principalTable: "mission_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mission_stage_hints_MissionStageId",
                schema: "mission_management",
                table: "mission_stage_hints",
                column: "MissionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_mission_stages_MissionId_Order",
                schema: "mission_management",
                table: "mission_stages",
                columns: new[] { "MissionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_missions_Name",
                schema: "mission_management",
                table: "missions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_stage_hints",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "mission_stages",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "missions",
                schema: "mission_management");
        }
    }
}
