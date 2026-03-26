using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class typeformulaire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssignationsExperts_OffreId",
                table: "AssignationsExperts");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "ChampsPersonnalises",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_AssignationsExperts_OffreId_ExpertId",
                table: "AssignationsExperts",
                columns: new[] { "OffreId", "ExpertId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssignationsExperts_OffreId_ExpertId",
                table: "AssignationsExperts");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "ChampsPersonnalises",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_AssignationsExperts_OffreId",
                table: "AssignationsExperts",
                column: "OffreId");
        }
    }
}
