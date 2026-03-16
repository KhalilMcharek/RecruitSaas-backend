// Services/AdminService.cs
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
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
        // LISTE DE TOUTES LES DEMANDES
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
                    Status = t.Statut.ToString()   // enum → string pour le DTO
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
                Status = entreprise.Tenant?.Statut.ToString(),  // enum → string
                CreatedAt = entreprise.CreeLe
            };
        }

        // ============================================================
        // APPROUVER → Tenant devient Approved
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

            if (entreprise.Tenant.Statut != TenantStatus.Pending)
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = TenantStatus.Approved;
            await _context.SaveChangesAsync();

            return (true, $"'{entreprise.Nom}' a été approuvée", new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut.ToString()
            });
        }

        // ============================================================
        // REJETER → Tenant devient Rejected
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

            if (entreprise.Tenant.Statut != TenantStatus.Pending)
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = TenantStatus.Rejected;
            await _context.SaveChangesAsync();

            return (true, $"'{entreprise.Nom}' a été rejetée", new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut.ToString()
            });
        }

        // ============================================================
        // STATISTIQUES DASHBOARD
        // ============================================================
        public async Task<object> GetDashboardStatsAsync()
        {
            var tenants = await _context.Tenants.ToListAsync();

            return new
            {
                total = tenants.Count,
                pending = tenants.Count(t => t.Statut == TenantStatus.Pending),
                approved = tenants.Count(t => t.Statut == TenantStatus.Approved),
                rejected = tenants.Count(t => t.Statut == TenantStatus.Rejected)
            };
        }
    }
}