using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddEntretienIAQuestionsAndLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Langue",
                table: "EntretiensIA",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QuestionsJson",
                table: "EntretiensIA",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Langue",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "QuestionsJson",
                table: "EntretiensIA");
        }
    }
}
