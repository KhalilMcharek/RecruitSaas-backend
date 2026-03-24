using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class MergeExpertFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Poste",
                table: "Experts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UtilisateurId",
                table: "Experts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Experts_UtilisateurId",
                table: "Experts",
                column: "UtilisateurId");

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Utilisateurs_UtilisateurId",
                table: "Experts",
                column: "UtilisateurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Utilisateurs_UtilisateurId",
                table: "Experts");

            migrationBuilder.DropIndex(
                name: "IX_Experts_UtilisateurId",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "Poste",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "UtilisateurId",
                table: "Experts");
        }
    }
}
