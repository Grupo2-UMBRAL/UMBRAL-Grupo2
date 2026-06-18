using Microsoft.EntityFrameworkCore.Migrations;

namespace MissionManagement.Infrastructure.Persistence.Migrations;

public partial class AddMissionStageDifficulty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Difficulty",
            schema: "mission_management",
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
            schema: "mission_management",
            table: "mission_stages");
    }
}
