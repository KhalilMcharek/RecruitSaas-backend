using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class refactorinterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentQuestionIndex",
                table: "EntretiensIA",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "EntretiensIA",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "EntretiensIA",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "EntretiensIA",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentQuestionIndex",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "EntretiensIA");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "EntretiensIA");
        }
    }
}
