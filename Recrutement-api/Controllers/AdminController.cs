// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.TenantRequest;
using Recrutement_api.Services;
using Recrutement_api.DTOs.Admin;


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

        // ============================================================
        // GET: api/admin/tenants-from-files
        // Pour TenantManagement.vue - Lit les tenants depuis les fichiers JSON
        // ============================================================
        [HttpGet("tenants-from-files")]
        public async Task<IActionResult> GetTenantsFromFiles()
        {
            try
            {
                var tenants = await _adminService.GetTenantsFromJsonFilesAsync();
                return Ok(new { success = true, count = tenants.Count, data = tenants });
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET: api/admin/tenants
        // Pour TenantManagement.vue - Liste des tenants approuvés (depuis DB)
        // ============================================================
        [HttpGet("tenants")]
        public async Task<IActionResult> GetTenants([FromQuery] string? status = null)
        {
            try
            {
                var tenants = await _adminService.GetAllTenantsAsync(status);
                return Ok(new { success = true, count = tenants.Count, data = tenants });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // PATCH: api/admin/tenants/{rne}/suspend
        // Pour suspendre un tenant (le marquer comme rejeté)
        // ============================================================
        [HttpPatch("tenants/{rne}/suspend")]
        public async Task<IActionResult> SuspendTenant(string rne)
        {
            try
            {
                var (success, message) = await _adminService.SuspendTenantAsync(rne);
                if (!success)
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET: api/admin/tenants/{rne}
        // Détail complet d'un tenant par RNE (depuis DB)
        // ============================================================
        [HttpGet("tenants/{rne}")]
        public async Task<IActionResult> GetTenantByRne(string rne)
        {
            try
            {
                var tenant = await _adminService.GetTenantByRneAsync(rne);
                if (tenant == null)
                    return NotFound(new { success = false, message = $"Tenant avec RNE '{rne}' non trouvé" });

                return Ok(new { success = true, data = tenant });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // NOUVEAUX ENDPOINTS POUR LE PROFIL ADMIN
        // ============================================================

        // GET: api/admin/profile
        // Récupère le profil de l'admin connecté (depuis la table Utilisateurs)
        [HttpGet("profile")]
        public async Task<IActionResult> GetAdminProfile()
        {
            try
            {
                // Récupérer l'ID de l'utilisateur depuis le token JWT
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst("nameid")?.Value
                                  ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                {
                    return Unauthorized(new { success = false, message = "Utilisateur non identifié" });
                }

                var profile = await _adminService.GetAdminProfileAsync(userId);
                if (profile == null)
                    return NotFound(new { success = false, message = "Profil admin non trouvé" });

                return Ok(new { success = true, data = profile });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // PUT: api/admin/profile
        // Met à jour le profil de l'admin connecté
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateAdminProfile([FromBody] UpdateAdminProfileDto updateDto)
        {
            try
            {
                // Récupérer l'ID de l'utilisateur depuis le token JWT
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst("nameid")?.Value
                                  ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                {
                    return Unauthorized(new { success = false, message = "Utilisateur non identifié" });
                }

                var (success, message) = await _adminService.UpdateAdminProfileAsync(userId, updateDto);
                if (!success)
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: api/admin/change-password
        // Change le mot de passe de l'admin connecté
        // POST: api/admin/change-password
        // Change le mot de passe de l'admin connecté
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangeAdminPassword([FromBody] AdminChangePasswordDto changePasswordDto)
        {
            try
            {
                // Récupérer l'ID de l'utilisateur depuis le token JWT
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst("nameid")?.Value
                                  ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                {
                    return Unauthorized(new { success = false, message = "Utilisateur non identifié" });
                }

                var (success, message) = await _adminService.ChangeAdminPasswordAsync(userId, changePasswordDto);
                if (!success)
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}