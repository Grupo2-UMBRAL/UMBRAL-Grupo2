using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbral.MissionDesign.Api.Infrastructure.Migrations;

public partial class AddMissionStageDifficulty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Difficulty",
            schema: "mission_design",
            table: "mission_stages",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "Easy");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Difficulty",
            schema: "mission_design",
            table: "mission_stages");
    }
}
