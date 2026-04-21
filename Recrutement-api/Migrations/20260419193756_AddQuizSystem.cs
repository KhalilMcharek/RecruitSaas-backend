using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizSystem : Migration
    {
        /// <inheritdoc />
       // Migrations/XXXXXX_AddQuizSystem.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
   
    migrationBuilder.AddColumn<string>(
        name: "Metadata",
        table: "CandidatNotifications",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "QuizToken",
        table: "CandidatNotifications",
        type: "text",
        nullable: true);



    migrationBuilder.CreateTable(
        name: "Quizzes",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CandidatureId = table.Column<Guid>(type: "uuid", nullable: false),
            QuizToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            TimePerQuestion = table.Column<int>(type: "integer", nullable: false),
            Instructions = table.Column<string>(type: "text", nullable: true),
            QuestionsJson = table.Column<string>(type: "text", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_Quizzes", x => x.Id);
            table.ForeignKey(
                name: "FK_Quizzes_Candidatures_CandidatureId",
                column: x => x.CandidatureId,
                principalTable: "Candidatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_Quizzes_CandidatureId",
        table: "Quizzes",
        column: "CandidatureId");

    migrationBuilder.CreateIndex(
        name: "IX_Quizzes_QuizToken",
        table: "Quizzes",
        column: "QuizToken",
        unique: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(name: "Quizzes");

    migrationBuilder.DropColumn(name: "Metadata", table: "CandidatNotifications");
    migrationBuilder.DropColumn(name: "QuizToken", table: "CandidatNotifications");

}}}