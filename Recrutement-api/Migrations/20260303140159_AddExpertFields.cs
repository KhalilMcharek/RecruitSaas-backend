using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recrutement_api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Entreprises_Tenants_TenantId",
                table: "Entreprises");

            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Entreprises_EntrepriseId",
                table: "Experts");

            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Utilisateurs_UtilisateurId",
                table: "Experts");

            migrationBuilder.DropForeignKey(
                name: "FK_OffresEmploi_Entreprises_EntrepriseId",
                table: "OffresEmploi");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Entreprises",
                table: "Entreprises");

            migrationBuilder.RenameTable(
                name: "Entreprises",
                newName: "Entreprise");

            migrationBuilder.RenameColumn(
                name: "UtilisateurId",
                table: "Experts",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "Poste",
                table: "Experts",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_Experts_UtilisateurId",
                table: "Experts",
                newName: "IX_Experts_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Entreprises_TenantId",
                table: "Entreprise",
                newName: "IX_Entreprise_TenantId");

            migrationBuilder.AlterColumn<Guid>(
                name: "EntrepriseId",
                table: "Experts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Experts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Experts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Experts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Experts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvited",
                table: "Experts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Experts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Experts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Experts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Entreprise",
                table: "Entreprise",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Experts_CompanyId",
                table: "Experts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Experts_Email",
                table: "Experts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Experts_Email_TenantId",
                table: "Experts",
                columns: new[] { "Email", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Entreprise_Tenants_TenantId",
                table: "Entreprise",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Entreprise_CompanyId",
                table: "Experts",
                column: "CompanyId",
                principalTable: "Entreprise",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Entreprise_EntrepriseId",
                table: "Experts",
                column: "EntrepriseId",
                principalTable: "Entreprise",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Tenants_TenantId",
                table: "Experts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OffresEmploi_Entreprise_EntrepriseId",
                table: "OffresEmploi",
                column: "EntrepriseId",
                principalTable: "Entreprise",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Entreprise_Tenants_TenantId",
                table: "Entreprise");

            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Entreprise_CompanyId",
                table: "Experts");

            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Entreprise_EntrepriseId",
                table: "Experts");

            migrationBuilder.DropForeignKey(
                name: "FK_Experts_Tenants_TenantId",
                table: "Experts");

            migrationBuilder.DropForeignKey(
                name: "FK_OffresEmploi_Entreprise_EntrepriseId",
                table: "OffresEmploi");

            migrationBuilder.DropIndex(
                name: "IX_Experts_CompanyId",
                table: "Experts");

            migrationBuilder.DropIndex(
                name: "IX_Experts_Email",
                table: "Experts");

            migrationBuilder.DropIndex(
                name: "IX_Experts_Email_TenantId",
                table: "Experts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Entreprise",
                table: "Entreprise");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "IsInvited",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Experts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Experts");

            migrationBuilder.RenameTable(
                name: "Entreprise",
                newName: "Entreprises");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Experts",
                newName: "UtilisateurId");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Experts",
                newName: "Poste");

            migrationBuilder.RenameIndex(
                name: "IX_Experts_TenantId",
                table: "Experts",
                newName: "IX_Experts_UtilisateurId");

            migrationBuilder.RenameIndex(
                name: "IX_Entreprise_TenantId",
                table: "Entreprises",
                newName: "IX_Entreprises_TenantId");

            migrationBuilder.AlterColumn<Guid>(
                name: "EntrepriseId",
                table: "Experts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Entreprises",
                table: "Entreprises",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entreprises_Tenants_TenantId",
                table: "Entreprises",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Entreprises_EntrepriseId",
                table: "Experts",
                column: "EntrepriseId",
                principalTable: "Entreprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Experts_Utilisateurs_UtilisateurId",
                table: "Experts",
                column: "UtilisateurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OffresEmploi_Entreprises_EntrepriseId",
                table: "OffresEmploi",
                column: "EntrepriseId",
                principalTable: "Entreprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
