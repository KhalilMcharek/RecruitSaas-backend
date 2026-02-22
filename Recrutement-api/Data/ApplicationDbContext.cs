using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Recrutement_api.DTOs;
using Recrutement_api.Models;

namespace Recrutement_api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Entreprise> Entreprises { get; set; }
        public DbSet<Expert> Experts { get; set; }
        public DbSet<Candidat> Candidats { get; set; }
        public DbSet<OffreEmploi> OffresEmploi { get; set; }
        public DbSet<FormulaireCandidature> FormulairesCandidature { get; set; }
        public DbSet<Candidature> Candidatures { get; set; }
        public DbSet<AssignationExpert> AssignationsExperts { get; set; }
        public DbSet<AvisExpert> AvisExperts { get; set; }
        public DbSet<AnalyseCV> AnalysesCV { get; set; }
        public DbSet<EntretienIA> EntretiensIA { get; set; }


protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Convertit l'enum RoleUtilisateur en texte dans la DB
    var roleConverter = new ValueConverter<RoleUtilisateur, string>(
        v => v.ToString(), // C# enum -> string pour la DB
        v => (RoleUtilisateur)Enum.Parse(typeof(RoleUtilisateur), v) // string DB -> C# enum
    );

    modelBuilder.Entity<Utilisateur>()
        .Property(u => u.Role)
        .HasConversion(roleConverter)   // conversion enum <-> texte
        .HasMaxLength(20)               // limite taille du texte dans la DB
        .IsRequired();

    // Convertit l'enum TenantStatus en texte dans la DB
    var tenantStatusConverter = new ValueConverter<TenantStatus, string>(
        v => v.ToString(), // C# enum -> string pour la DB
        v => (TenantStatus)Enum.Parse(typeof(TenantStatus), v) // string DB -> C# enum
    );

    modelBuilder.Entity<Tenant>()
        .Property(t => t.Statut)
        .HasConversion(tenantStatusConverter)
        .HasMaxLength(20)
        .IsRequired();


         modelBuilder.Entity<Candidat>()
        .Property(c => c.Telephone)
        .HasMaxLength(50)
        .IsRequired()
        .HasDefaultValue("Non précisé");
}}}