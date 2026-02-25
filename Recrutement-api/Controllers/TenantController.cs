// Controllers/TenantController.cs
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Tenant;
using Recrutement_api.Services;
using Recrutement_api.Data;
using Microsoft.EntityFrameworkCore;

namespace Recrutement_api.Controllers
{
    [Route("api/tenant")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly TenantService _tenantService;
        private readonly ApplicationDbContext _context;

        public TenantController(TenantService tenantService, ApplicationDbContext context)
        {
            _tenantService = tenantService;
            _context = context;
        }

        // Helper pour obtenir l'ID du tenant connecté
        private Guid? GetCurrentTenantId()
        {
            var premierTenant = _context.Tenants.FirstOrDefault();
            if (premierTenant != null)
            {
                Console.WriteLine($"✅ Test: Utilisation du tenant {premierTenant.Nom} (ID: {premierTenant.Id})");
                return premierTenant.Id;
            }
            
            Console.WriteLine("❌ Aucun tenant trouvé dans la base");
            return null;
        }

        // ============================================
        // GET /api/tenant/companies
        // Liste toutes les entreprises du tenant
        // ============================================
        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Tenant non trouvé" });

            var companies = await _tenantService.GetCompaniesAsync(tenantId.Value);
            return Ok(new { success = true, data = companies });
        }

        // ============================================
        // GET /api/tenant/companies/{id}
        // Détail d'une entreprise
        // ============================================
        [HttpGet("companies/{id}")]
        public async Task<IActionResult> GetCompany(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Tenant non trouvé" });

            var company = await _tenantService.GetCompanyByIdAsync(id, tenantId.Value);
            if (company == null)
                return NotFound(new { success = false, message = "Entreprise non trouvée" });

            return Ok(new { success = true, data = company });
        }

        // ============================================
        // POST /api/tenant/companies
        // Créer une nouvelle entreprise (avec logo optionnel)
        // ============================================
        [HttpPost("companies")]
        public async Task<IActionResult> CreateCompany([FromForm] CreateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Tenant non trouvé" });

            try
            {
                var company = await _tenantService.CreateCompanyAsync(tenantId.Value, dto, logo);

                return Ok(new
                {
                    success = true,
                    message = "Entreprise créée avec succès, en attente d'approbation",
                    data = new { company.Id, company.Nom, company.LogoUrl, company.Status }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // PUT /api/tenant/companies/{id}
        // Modifier une entreprise (infos + logo optionnel)
        // ============================================
        [HttpPut("companies/{id}")]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromForm] UpdateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Tenant non trouvé" });

            var (success, message, data) = await _tenantService.UpdateCompanyAsync(id, tenantId.Value, dto, logo);
            
            if (!success)
                return NotFound(new { success = false, message });

            return Ok(new { success = true, message, data });
        }

        // ============================================
        // DELETE /api/tenant/companies/{id}
        // Supprimer une entreprise
        // ============================================
        [HttpDelete("companies/{id}")]
        public async Task<IActionResult> DeleteCompany(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Tenant non trouvé" });

            var (success, message, data) = await _tenantService.DeleteCompanyAsync(id, tenantId.Value);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, data });
        }
    }
}