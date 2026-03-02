// Services/AdminService.cs
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.TenantRequest;

namespace Recrutement_api.Services
{
    public class AdminService
    {
        private readonly ApplicationDbContext _context;

        public AdminService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // LISTE DE TOUTES LES DEMANDES (tenants en Pending)
        // ============================================================
        public async Task<List<TenantRequestListDto>> GetAllTenantRequestsAsync()
        {
            return await _context.Tenants
                .Include(t => t.Utilisateur)
                .Include(t => t.Entreprises)
                .OrderByDescending(t => t.CreeLe)
                .Select(t => new TenantRequestListDto
                {
                    CompanyName = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.Nom).FirstOrDefault(),
                    RNE = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.RNE).FirstOrDefault(),
                    Owner = t.Utilisateur != null ? t.Utilisateur.Nom : null,
                    Industry = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.Secteur).FirstOrDefault(),
                    Status = t.Statut
                })
                .ToListAsync();
        }

        // ============================================================
        // DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // ============================================================
        public async Task<TenantRequestDetailDto?> GetTenantRequestByNameAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                    .ThenInclude(t => t.Utilisateur)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return null;

            return new TenantRequestDetailDto
            {
                Id = entreprise.Id,
                CompanyName = entreprise.Nom,
                RNE = entreprise.RNE,
                Owner = entreprise.Tenant?.Utilisateur?.Nom,
                WorkEmail = entreprise.Tenant?.Utilisateur?.Email,
                Industry = entreprise.Secteur,
                Description = entreprise.Description,
                LogoUrl = entreprise.LogoUrl,
                Status = entreprise.Tenant?.Statut,
                CreatedAt = entreprise.CreeLe
            };
        }

        // ============================================================
        // APPROUVER UNE DEMANDE → Tenant devient Actif
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> ApproveTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Tenant == null)
                return (false, "Tenant associé introuvable", null);

            if (entreprise.Tenant.Statut != "Pending")
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = "Actif";
            await _context.SaveChangesAsync();

            var dto = new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut
            };

            return (true, $"'{entreprise.Nom}' a été approuvée, le tenant est maintenant Actif", dto);
        }

        // ============================================================
        // REJETER UNE DEMANDE → Tenant devient Inactif
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> RejectTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Tenant == null)
                return (false, "Tenant associé introuvable", null);

            if (entreprise.Tenant.Statut != "Pending")
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = "Inactif";
            await _context.SaveChangesAsync();

            var dto = new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut
            };

            return (true, $"'{entreprise.Nom}' a été rejetée, le tenant est Inactif", dto);
        }

        // ============================================================
        // STATISTIQUES POUR LE DASHBOARD
        // ============================================================
        public async Task<object> GetDashboardStatsAsync()
        {
            var tenants = await _context.Tenants.ToListAsync();

            return new
            {
                total = tenants.Count,
                pending = tenants.Count(t => t.Statut == "Pending"),
                approved = tenants.Count(t => t.Statut == "Actif"),
                rejected = tenants.Count(t => t.Statut == "Inactif")
            };
        }
    }
}