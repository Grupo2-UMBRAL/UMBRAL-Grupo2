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
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.Id);
                });

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
                name: "missions",
                schema: "mission_management");
        }
    }
}
