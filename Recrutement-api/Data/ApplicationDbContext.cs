using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Recrutement_api.DTOs;
using Recrutement_api.Models;

namespace Recrutement_api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Entreprise> Entreprises { get; set; }
        public DbSet<Entreprise> Companies => Entreprises;
        public DbSet<Expert> Experts { get; set; }
        public DbSet<Candidat> Candidats { get; set; }
        public DbSet<OffreEmploi> OffresEmploi { get; set; }
        public DbSet<FormulaireCandidature> FormulairesCandidature { get; set; }
        public DbSet<Candidature>           Candidatures           { get; set; }
        public DbSet<AssignationExpert>     AssignationsExperts    { get; set; }
        public DbSet<AvisExpert>            AvisExperts            { get; set; }
        public DbSet<ChampPersonnalise>     ChampsPersonnalises    { get; set; }
        public DbSet<AnalyseCV>             AnalysesCV             { get; set; }
        public DbSet<EntretienIA>           EntretiensIA           { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes
            modelBuilder.Entity<Utilisateur>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Candidature>().HasIndex(c => c.OffreId);
            modelBuilder.Entity<Candidature>().HasIndex(c => c.CandidatId);
            modelBuilder.Entity<OffreEmploi>().HasIndex(o => o.EntrepriseId);

            // Enum converters
            var roleConverter = new EnumToStringConverter<RoleUtilisateur>();
            var tenantStatusConverter = new EnumToStringConverter<TenantStatus>();
            modelBuilder.Entity<Utilisateur>().Property(u => u.Role).HasConversion(roleConverter);
            modelBuilder.Entity<Tenant>().Property(t => t.Statut).HasConversion(tenantStatusConverter);
            modelBuilder.Entity<Expert>().Property(e => e.Role).HasConversion<string>();
            modelBuilder.Entity<Expert>().Property(e => e.Status).HasConversion<string>();
            modelBuilder.Entity<OffreEmploi>().Property(e => e.TypeContrat).HasConversion<string>();

            // Expert indexes
            modelBuilder.Entity<Expert>().HasIndex(e => e.Email);
            modelBuilder.Entity<Expert>().HasIndex(e => new { e.Email, e.TenantId }).IsUnique();

            // Relations
            modelBuilder.Entity<Candidat>()
                .HasOne(c => c.Utilisateur).WithOne()
                .HasForeignKey<Candidat>(c => c.UtilisateurId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.Utilisateur).WithOne()
                .HasForeignKey<Tenant>(t => t.UtilisateurId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Entreprise>()
                .HasOne(e => e.Tenant).WithMany(t => t.Entreprises)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Expert>()
                .HasOne(e => e.Tenant).WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Expert>()
                .HasOne(e => e.Company).WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OffreEmploi>()
                .HasOne(o => o.Formulaire)
                .WithOne(f => f.Offre)
                .HasForeignKey<FormulaireCandidature>(f => f.OffreId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChampPersonnalise>()
                .Property(c => c.Type)
                .HasConversion<string>();

            modelBuilder.Entity<ChampPersonnalise>()
                .HasOne(c => c.Formulaire)
                .WithMany(f => f.ChampsPersonnalises)
                .HasForeignKey(c => c.FormulaireId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssignationExpert>()
                .HasIndex(a => new { a.OffreId, a.ExpertId })
                .IsUnique();
            modelBuilder.Entity<AssignationExpert>()
    .HasOne(a => a.Expert)
    .WithMany(e => e.Assignations)
    .HasForeignKey(a => a.ExpertId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<AvisExpert>()
    .HasOne(a => a.Expert)
    .WithMany(e => e.Avis)
    .HasForeignKey(a => a.ExpertId)
    .OnDelete(DeleteBehavior.Cascade);
        }

        public override int SaveChanges() { StampUpdatedAt(); return base.SaveChanges(); }
        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken ct = default) { StampUpdatedAt(); return base.SaveChangesAsync(ct); }
        private void StampUpdatedAt() { foreach (var e in ChangeTracker.Entries<Expert>()) if (e.State == EntityState.Modified) e.Entity.UpdatedAt = DateTime.UtcNow; }
    }
}