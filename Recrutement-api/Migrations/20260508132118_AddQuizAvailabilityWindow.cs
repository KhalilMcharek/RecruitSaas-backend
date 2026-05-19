using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizAvailabilityWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScheduledDate",
                table: "Quizzes",
                newName: "Deadline");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                table: "Quizzes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Quizzes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Quizzes");

            migrationBuilder.RenameColumn(
                name: "Deadline",
                table: "Quizzes",
                newName: "ScheduledDate");
        }
    }
}
