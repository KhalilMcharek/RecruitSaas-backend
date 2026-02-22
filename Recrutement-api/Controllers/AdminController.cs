using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.TenantRequest;

namespace Recrutement_api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1️⃣  LISTE DE TOUTES LES DEMANDES
        // GET: api/admin/tenant-requests
        // ============================================================
        [HttpGet("tenant-requests")]
        public async Task<IActionResult> GetAllTenantRequests()
        {
            try
            {
                var requests = await _context.Entreprises
                    .Include(e => e.Tenant)
                    .OrderByDescending(e => e.CreeLe)
                    .Select(e => new TenantRequestListDto
                    {
                        CompanyName = e.Nom,
                        RNE         = e.RNE,
                        Owner       = e.Tenant.Nom,      // Nom du Tenant
                        Industry    = e.Secteur,          // Secteur de l'entreprise
                        Status      = e.Status
                    })
                    .ToListAsync();

                return Ok(new { success = true, count = requests.Count, data = requests });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 2️⃣  DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // GET: api/admin/tenant-requests/by-name/{companyName}
        // ============================================================
        [HttpGet("tenant-requests/by-name/{companyName}")]
        public async Task<IActionResult> GetTenantRequestByName(string companyName)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(companyName);

                var entreprise = await _context.Entreprises
                    .Include(e => e.Tenant)
                        .ThenInclude(t => t.Utilisateur)
                    .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

                if (entreprise == null)
                    return NotFound(new { success = false, message = $"Aucune entreprise trouvée pour '{decoded}'" });

                var dto = new TenantRequestDetailDto
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

                return Ok(new { success = true, data = dto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 3️⃣  APPROUVER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/approve
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/approve")]
        public async Task<IActionResult> ApproveTenantRequest(string companyName)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(companyName);

                var entreprise = await _context.Entreprises
                    .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

                if (entreprise == null)
                    return NotFound(new { success = false, message = $"Aucune entreprise trouvée pour '{decoded}'" });

                if (entreprise.Status != "Pending")
                    return BadRequest(new { success = false, message = $"Cette demande a déjà été traitée (statut : {entreprise.Status})" });

                entreprise.Status = "Approved";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"'{entreprise.Nom}' a été approuvée",
                    data    = new TenantStatusResponseDto { CompanyName = entreprise.Nom, Status = entreprise.Status }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 4️⃣  REJETER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/reject
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/reject")]
        public async Task<IActionResult> RejectTenantRequest(string companyName)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(companyName);

                var entreprise = await _context.Entreprises
                    .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

                if (entreprise == null)
                    return NotFound(new { success = false, message = $"Aucune entreprise trouvée pour '{decoded}'" });

                if (entreprise.Status != "Pending")
                    return BadRequest(new { success = false, message = $"Cette demande a déjà été traitée (statut : {entreprise.Status})" });

                entreprise.Status = "Rejected";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"'{entreprise.Nom}' a été rejetée",
                    data    = new TenantStatusResponseDto { CompanyName = entreprise.Nom, Status = entreprise.Status }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}