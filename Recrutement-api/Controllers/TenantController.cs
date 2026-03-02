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

        // ============================================
        // Helper : retourne le premier tenant Actif
        // Si aucun tenant Actif → retourne null
        // ============================================
        private Guid? GetCurrentTenantId()
        {
            var tenantActif = _context.Tenants
                .Include(t => t.Utilisateur)
                .FirstOrDefault(t => t.Statut == "Actif");

            if (tenantActif != null)
            {
                Console.WriteLine($"✅ Tenant actif trouvé : {tenantActif.Utilisateur?.Nom} (ID: {tenantActif.Id})");
                return tenantActif.Id;
            }

            Console.WriteLine("❌ Aucun tenant actif trouvé dans la base");
            return null;
        }

        // ============================================
        // GET /api/tenant/companies
        // ============================================
        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Ok(new { success = false, message = "Aucun tenant approuvé disponible", statut = (string?)null });

            var (success, message, data, statut) = await _tenantService.GetCompaniesAsync(tenantId.Value);

            if (!success)
                return Ok(new { success = false, message, statut });

            return Ok(new { success = true, data, statut });
        }

        // ============================================
        // GET /api/tenant/companies/{id}
        // ============================================
        [HttpGet("companies/{id}")]
        public async Task<IActionResult> GetCompany(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Ok(new { success = false, message = "Aucun tenant approuvé disponible" });

            var company = await _tenantService.GetCompanyByIdAsync(id, tenantId.Value);
            if (company == null)
                return NotFound(new { success = false, message = "Entreprise non trouvée" });

            return Ok(new { success = true, data = company });
        }

        // ============================================
        // POST /api/tenant/companies
        // ============================================
        [HttpPost("companies")]
        public async Task<IActionResult> CreateCompany([FromForm] CreateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Ok(new { success = false, message = "Aucun tenant approuvé disponible" });

            var (success, message, company) = await _tenantService.CreateCompanyAsync(tenantId.Value, dto, logo);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new
            {
                success = true,
                message,
                data = new { company!.Id, company.Nom, company.LogoUrl }
            });
        }

        // ============================================
        // PUT /api/tenant/companies/{id}
        // ============================================
        [HttpPut("companies/{id}")]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromForm] UpdateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Ok(new { success = false, message = "Aucun tenant approuvé disponible" });

            var (success, message, data) = await _tenantService.UpdateCompanyAsync(id, tenantId.Value, dto, logo);

            if (!success)
                return NotFound(new { success = false, message });

            return Ok(new { success = true, message, data });
        }

        // ============================================
        // DELETE /api/tenant/companies/{id}
        // ============================================
        [HttpDelete("companies/{id}")]
        public async Task<IActionResult> DeleteCompany(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (tenantId == null)
                return Ok(new { success = false, message = "Aucun tenant approuvé disponible" });

            var (success, message, data) = await _tenantService.DeleteCompanyAsync(id, tenantId.Value);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, data });
        }
    }
}