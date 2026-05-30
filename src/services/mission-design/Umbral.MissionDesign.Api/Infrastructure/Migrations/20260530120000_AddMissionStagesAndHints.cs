using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbral.MissionDesign.Api.Infrastructure.Migrations
{
    public partial class AddMissionStagesAndHints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mission_stages",
                schema: "mission_design",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                        principalSchema: "mission_design",
                        principalTable: "missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_stage_hints",
                schema: "mission_design",
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
                        principalSchema: "mission_design",
                        principalTable: "mission_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mission_stages_MissionId_Order",
                schema: "mission_design",
                table: "mission_stages",
                columns: new[] { "MissionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_stages_MissionId",
                schema: "mission_design",
                table: "mission_stages",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_mission_stage_hints_MissionStageId",
                schema: "mission_design",
                table: "mission_stage_hints",
                column: "MissionStageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_stage_hints",
                schema: "mission_design");

            migrationBuilder.DropTable(
                name: "mission_stages",
                schema: "mission_design");
        }
    }
}
