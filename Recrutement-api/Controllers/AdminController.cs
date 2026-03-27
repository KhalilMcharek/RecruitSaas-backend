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
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/approve
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/approve")]
        public async Task<IActionResult> ApproveTenantRequest(string companyName)
        {
            try
            {
                var (success, message, data) = await _adminService.ApproveTenantRequestAsync(companyName);
                if (!success)
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/reject
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/reject")]
        public async Task<IActionResult> RejectTenantRequest(string companyName)
        {
            try
            {
                var (success, message, data) = await _adminService.RejectTenantRequestAsync(companyName);
                if (!success)
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET: api/admin/dashboard
        // ============================================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var stats = await _adminService.GetDashboardStatsAsync();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}