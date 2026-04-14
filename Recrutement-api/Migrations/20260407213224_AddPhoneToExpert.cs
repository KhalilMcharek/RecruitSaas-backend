using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneToExpert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Experts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Experts");
        }
    }
}
