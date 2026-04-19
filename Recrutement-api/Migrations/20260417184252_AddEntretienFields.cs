using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddEntretienFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreneauxDisponibles",
                table: "EntretiensIA",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LienActif",
                table: "EntretiensIA",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LienToken",
                table: "EntretiensIA",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MisAJourLe",
                table: "EntretiensIA",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanifiePar",
                table: "EntretiensIA",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreneauxDisponibles",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "LienActif",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "LienToken",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "MisAJourLe",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "PlanifiePar",
                table: "EntretiensIA");
        }
    }
}
