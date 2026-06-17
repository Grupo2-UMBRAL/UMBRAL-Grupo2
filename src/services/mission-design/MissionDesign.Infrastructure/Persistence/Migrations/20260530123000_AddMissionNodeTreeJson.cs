using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MissionDesign.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionNodeTreeJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NodeTreeJson",
                schema: "mission_design",
                table: "missions",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NodeTreeJson",
                schema: "mission_design",
                table: "missions");
        }
    }
}
