using Microsoft.EntityFrameworkCore;
using Recrutement_api.Models;

namespace Recrutement_api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Auth
        public DbSet<Utilisateur> Utilisateurs { get; set; }

        // Tenant
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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Utilisateur>()
                .HasIndex(u => u.Email)
                .IsUnique();
            modelBuilder.Entity<Utilisateur>()
                 .Property(u => u.Role)
                   .HasConversion<string>();

            modelBuilder.Entity<Candidature>()
                .HasIndex(c => c.OffreId);

            modelBuilder.Entity<Candidature>()
                .HasIndex(c => c.CandidatId);

            modelBuilder.Entity<OffreEmploi>()
                .HasIndex(o => o.EntrepriseId);
        }
    }
}
