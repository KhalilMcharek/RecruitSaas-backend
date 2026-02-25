// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.TenantRequest;
using Recrutement_api.Services;

namespace Recrutement_api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AdminService _adminService;

        public AdminController(AdminService adminService)
        {
            _adminService = adminService;
        }

        // ============================================================
        // 1️⃣ LISTE DE TOUTES LES DEMANDES
        // GET: api/admin/tenant-requests
        // ============================================================
        [HttpGet("tenant-requests")]
        public async Task<IActionResult> GetAllTenantRequests()
        {
            try
            {
                var requests = await _adminService.GetAllTenantRequestsAsync();
                return Ok(new { success = true, count = requests.Count, data = requests });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 2️⃣ DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // GET: api/admin/tenant-requests/by-name/{companyName}
        // ============================================================
        [HttpGet("tenant-requests/by-name/{companyName}")]
        public async Task<IActionResult> GetTenantRequestByName(string companyName)
        {
            try
            {
                var request = await _adminService.GetTenantRequestByNameAsync(companyName);

                if (request == null)
                    return NotFound(new { success = false, message = $"Aucune entreprise trouvée pour '{companyName}'" });

                return Ok(new { success = true, data = request });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 3️⃣ APPROUVER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/approve
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/approve")]
        public async Task<IActionResult> ApproveTenantRequest(string companyName)
        {
            try
            {
                var (success, message, data) = await _adminService.ApproveTenantRequestAsync(companyName);

                if (!success)
                    return NotFound(new { success = false, message });

                return Ok(new { success = true, message, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // 4️⃣ REJETER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/reject
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/reject")]
        public async Task<IActionResult> RejectTenantRequest(string companyName)
        {
            try
            {
                var (success, message, data) = await _adminService.RejectTenantRequestAsync(companyName);

                if (!success)
                    return NotFound(new { success = false, message });

                return Ok(new { success = true, message, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        }
    }
