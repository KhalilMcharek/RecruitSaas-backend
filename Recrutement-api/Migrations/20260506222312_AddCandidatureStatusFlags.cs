using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidatureStatusFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableQuiz",
                table: "Candidatures",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Flags",
                table: "Candidatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Candidatures",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableQuiz",
                table: "Candidatures");

            migrationBuilder.DropColumn(
                name: "Flags",
                table: "Candidatures");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Candidatures");
        }
    }
}
