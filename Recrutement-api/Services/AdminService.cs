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
        // LISTE DE TOUTES LES DEMANDES
        // ============================================================
        public async Task<List<TenantRequestListDto>> GetAllTenantRequestsAsync()
        {
            return await _context.Entreprises
                .Include(e => e.Tenant)
                .OrderByDescending(e => e.CreeLe)
                .Select(e => new TenantRequestListDto
                {
                    CompanyName = e.Nom,
                    RNE         = e.RNE,
                    Owner       = e.Tenant != null ? e.Tenant.Nom : null,
                    Industry    = e.Secteur,
                    Status      = e.Status
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
                Id          = entreprise.Id,
                CompanyName = entreprise.Nom,
                RNE         = entreprise.RNE,
                Owner       = entreprise.Tenant?.Nom,
                WorkEmail   = entreprise.Tenant?.Utilisateur?.Email,
                Industry    = entreprise.Secteur,
                Description = entreprise.Description,
                LogoUrl     = entreprise.LogoUrl,
                Status      = entreprise.Status,
                CreatedAt   = entreprise.CreeLe
            };
        }

        // ============================================================
        // APPROUVER UNE DEMANDE
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> ApproveTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Status != "Pending")
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Status})", null);

            entreprise.Status = "Approved";
            await _context.SaveChangesAsync();

            var dto = new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Status
            };

            return (true, $"'{entreprise.Nom}' a été approuvée", dto);
        }

        // ============================================================
        // REJETER UNE DEMANDE
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> RejectTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Status != "Pending")
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Status})", null);

            entreprise.Status = "Rejected";
            await _context.SaveChangesAsync();

            var dto = new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Status
            };

            return (true, $"'{entreprise.Nom}' a été rejetée", dto);
        }

        // ============================================================
        // STATISTIQUES POUR LE DASHBOARD
        // ============================================================
        public async Task<object> GetDashboardStatsAsync()
        {
            var entreprises = await _context.Entreprises.ToListAsync();

            return new
            {
                total = entreprises.Count,
                pending = entreprises.Count(e => e.Status == "Pending"),
                approved = entreprises.Count(e => e.Status == "Approved"),
                rejected = entreprises.Count(e => e.Status == "Rejected")
            };
        }
    }
}