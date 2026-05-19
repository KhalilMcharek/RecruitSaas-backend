using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class QuizSecurityFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientFingerprint",
                table: "Quizzes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentAnswersJson",
                table: "Quizzes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Quizzes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Quizzes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientFingerprint",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "CurrentAnswersJson",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Quizzes");
        }
    }
}
