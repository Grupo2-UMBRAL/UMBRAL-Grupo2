using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceSubmissionSubmittedChoiceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedChoiceId",
                schema: "session_management",
                table: "evidence_submissions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedChoiceId",
                schema: "session_management",
                table: "evidence_submissions");
        }
    }
}
