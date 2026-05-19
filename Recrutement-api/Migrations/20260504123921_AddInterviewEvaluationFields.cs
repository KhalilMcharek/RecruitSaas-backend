using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewEvaluationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Strengths",
                table: "EntretiensIA",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "EntretiensIA",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weaknesses",
                table: "EntretiensIA",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Strengths",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "Weaknesses",
                table: "EntretiensIA");
        }
    }
}
