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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    maximum_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "path_items",
                schema: "mission_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_section_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    item_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    game_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    default_difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    default_time_limit_minutes = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_path_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_path_items_missions_mission_id",
                        column: x => x.mission_id,
                        principalSchema: "mission_management",
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_path_items_path_items_parent_section_id",
                        column: x => x.parent_section_id,
                        principalSchema: "mission_management",
                        principalTable: "path_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plays",
                schema: "mission_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    difficulty_override = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    time_limit_minutes_override = table.Column<int>(type: "integer", nullable: true),
                    play_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    clue = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    expected_qr_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plays", x => x.id);
                    table.ForeignKey(
                        name: "FK_plays_path_items_challenge_id",
                        column: x => x.challenge_id,
                        principalSchema: "mission_management",
                        principalTable: "path_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "choices",
                schema: "mission_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_choices", x => x.id);
                    table.ForeignKey(
                        name: "FK_choices_plays_question_id",
                        column: x => x.question_id,
                        principalSchema: "mission_management",
                        principalTable: "plays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hints",
                schema: "mission_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    search_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_solution = table.Column<bool>(type: "boolean", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hints", x => x.id);
                    table.ForeignKey(
                        name: "FK_hints_plays_search_id",
                        column: x => x.search_id,
                        principalSchema: "mission_management",
                        principalTable: "plays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_choices_question_id_order",
                schema: "mission_management",
                table: "choices",
                columns: new[] { "question_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hints_search_id_order",
                schema: "mission_management",
                table: "hints",
                columns: new[] { "search_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_missions_name",
                schema: "mission_management",
                table: "missions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_path_items_mission_id_parent_section_id_order",
                schema: "mission_management",
                table: "path_items",
                columns: new[] { "mission_id", "parent_section_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_path_items_parent_section_id",
                schema: "mission_management",
                table: "path_items",
                column: "parent_section_id");

            migrationBuilder.CreateIndex(
                name: "IX_plays_challenge_id_order",
                schema: "mission_management",
                table: "plays",
                columns: new[] { "challenge_id", "order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "choices",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "hints",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "plays",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "path_items",
                schema: "mission_management");

            migrationBuilder.DropTable(
                name: "missions",
                schema: "mission_management");
        }
    }
}
