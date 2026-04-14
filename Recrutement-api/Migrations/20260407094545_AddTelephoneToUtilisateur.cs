using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddTelephoneToUtilisateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Telephone",
                table: "Utilisateurs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Telephone",
                table: "Utilisateurs");
        }
    }
}
