using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;

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

        // ============================================
        // 1️⃣ TOUTES LES DEMANDES D'INSCRIPTION (champs spécifiques)
        // ============================================
        // GET: api/admin/tenant-requests
        [HttpGet("tenant-requests")]
        public async Task<IActionResult> GetAllTenantRequests()
        {
            try
            {
                var requests = await _context.TenantRequests
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.CompanyName,
                        r.RNE,
                        r.Owner,
                        r.Industry,
                        r.Status
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = requests.Count,
                    data = requests
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 2️⃣ DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // ============================================
        // GET: api/admin/tenant-requests/by-name/TechCorp
        [HttpGet("tenant-requests/by-name/{companyName}")]
        public async Task<IActionResult> GetTenantRequestByName(string companyName)
        {
            try
            {
                var decodedName = Uri.UnescapeDataString(companyName);

                var request = await _context.TenantRequests
                    .Where(r => r.CompanyName.ToLower() == decodedName.ToLower())
                    .Select(r => new
                    {
                        r.Id,
                        r.CompanyName,
                        r.RNE,
                        r.Owner,
                        r.WorkEmail,
                        r.Industry,
                        r.Status,
                        r.CreatedAt,
                        r.Password
                    })
                    .FirstOrDefaultAsync();

                if (request == null)
                {
                    return NotFound(new 
                    { 
                        success = false, 
                        message = $"Aucune entreprise trouvée avec le nom '{decodedName}'" 
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = request
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 3️⃣ APPROUVER UNE DEMANDE PAR NOM D'ENTREPRISE
        // ============================================
        // PATCH: api/admin/tenant-requests/by-name/TechCorp/approve
        [HttpPatch("tenant-requests/by-name/{companyName}/approve")]
        public async Task<IActionResult> ApproveTenantRequestByName(string companyName)
        {
            try
            {
                var decodedName = Uri.UnescapeDataString(companyName);

                var request = await _context.TenantRequests
                    .FirstOrDefaultAsync(r => r.CompanyName.ToLower() == decodedName.ToLower());

                if (request == null)
                {
                    return NotFound(new 
                    { 
                        success = false, 
                        message = $"Aucune entreprise trouvée avec le nom '{decodedName}'" 
                    });
                }

                if (request.Status != "Pending")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cette demande a déjà été traitée (statut actuel: {request.Status})"
                    });
                }

                request.Status = "Approved";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"L'entreprise '{request.CompanyName}' a été approuvée",
                    data = new
                    {
                        request.CompanyName,
                        request.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 4️⃣ REFUSER UNE DEMANDE PAR NOM D'ENTREPRISE
        // ============================================
        // PATCH: api/admin/tenant-requests/by-name/TechCorp/reject
        [HttpPatch("tenant-requests/by-name/{companyName}/reject")]
        public async Task<IActionResult> RejectTenantRequestByName(string companyName)
        {
            try
            {
                var decodedName = Uri.UnescapeDataString(companyName);

                var request = await _context.TenantRequests
                    .FirstOrDefaultAsync(r => r.CompanyName.ToLower() == decodedName.ToLower());

                if (request == null)
                {
                    return NotFound(new 
                    { 
                        success = false, 
                        message = $"Aucune entreprise trouvée avec le nom '{decodedName}'" 
                    });
                }

                if (request.Status != "Pending")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cette demande a déjà été traitée (statut actuel: {request.Status})"
                    });
                }

                request.Status = "Rejected";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"L'entreprise '{request.CompanyName}' a été refusée",
                    data = new
                    {
                        request.CompanyName,
                        request.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}