using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    Seeking = table.Column<string>(type: "text", nullable: true),
                    Education = table.Column<string>(type: "text", nullable: true),
                    FieldOfStudy = table.Column<string>(type: "text", nullable: true),
                    Experience = table.Column<string>(type: "text", nullable: true),
                    Availability = table.Column<string>(type: "text", nullable: true),
                    SkillsJson = table.Column<string>(type: "text", nullable: true),
                    Linkedin = table.Column<string>(type: "text", nullable: true),
                    Github = table.Column<string>(type: "text", nullable: true),
                    PortfolioUrl = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateProfiles_Candidats_CandidatId",
                        column: x => x.CandidatId,
                        principalTable: "Candidats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    Linkedin = table.Column<string>(type: "text", nullable: true),
                    Twitter = table.Column<string>(type: "text", nullable: true),
                    HiringStatus = table.Column<string>(type: "text", nullable: true),
                    WorkTypesJson = table.Column<string>(type: "text", nullable: true),
                    TechStackJson = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_CandidatId",
                table: "CandidateProfiles",
                column: "CandidatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProfiles_TenantId",
                table: "TenantProfiles",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropTable(
                name: "TenantProfiles");
        }
    }
}
