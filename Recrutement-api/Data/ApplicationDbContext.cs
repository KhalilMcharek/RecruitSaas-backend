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

        // ============================
        // Auth
        // ============================
        public DbSet<Utilisateur> Utilisateurs { get; set; }

        // ============================
        // Tenant
        // ============================
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

            // ============================
            // INDEXES
            // ============================
            modelBuilder.Entity<Utilisateur>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Candidature>()
                .HasIndex(c => c.OffreId);

            modelBuilder.Entity<Candidature>()
                .HasIndex(c => c.CandidatId);

            modelBuilder.Entity<OffreEmploi>()
                .HasIndex(o => o.EntrepriseId);

            // ============================
            // ENUM CONVERTERS (AJOUTS)
            // ============================

            var roleConverter = new EnumToStringConverter<RoleUtilisateur>();
            var tenantStatusConverter = new EnumToStringConverter<TenantStatus>();

            modelBuilder.Entity<Utilisateur>()
                .Property(u => u.Role)
                .HasConversion(roleConverter);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.Statut)
                .HasConversion(tenantStatusConverter);

            // ============================
            // RELATIONS IMPORTANTES (AJOUTS PROPRES)
            // ============================

            // Utilisateur - Candidat (1-1)
            modelBuilder.Entity<Candidat>()
                .HasOne(c => c.Utilisateur)
                .WithOne()
                .HasForeignKey<Candidat>(c => c.UtilisateurId)
                .OnDelete(DeleteBehavior.Cascade);

            // Utilisateur - Tenant (1-1)
            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.Utilisateur)
                .WithOne()
                .HasForeignKey<Tenant>(t => t.UtilisateurId)
                .OnDelete(DeleteBehavior.Cascade);

            // Tenant - Entreprise (1-N)
            modelBuilder.Entity<Entreprise>()
                .HasOne(e => e.Tenant)
                .WithMany(t => t.Entreprises)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}