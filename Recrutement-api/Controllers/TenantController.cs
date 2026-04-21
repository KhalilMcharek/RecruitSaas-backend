// Controllers/TenantController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Tenant;
using Recrutement_api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Recrutement_api.Services.TenantServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChangePasswordDto = Recrutement_api.DTOs.Tenant.ChangePasswordDto;
using Recrutement_api.Services;

namespace Recrutement_api.Controllers
{
    [Authorize]
    [Route("api/tenant")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly TenantService _tenantService;
        private readonly ApplicationDbContext _context;
        private readonly TenantProfileService _profileService;
private readonly IAuthService _authService;


        public TenantController(TenantService tenantService, ApplicationDbContext context, IAuthService authService,TenantProfileService profileService
)
        {
            _tenantService = tenantService;
            _context = context;
             _profileService = profileService;
            _authService  = authService;    // ← ajoute ça


            
            
        }

        // ============================================================
        // Helper : lit l'UserId depuis le JWT
        // ============================================================
        private Guid? GetCurrentUserId()
        {
            // Essaie tous les formats possibles du claim "sub" dans .NET
            var sub = User.FindFirstValue("sub")
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.TryParse(sub, out var userId))
            {
                return userId;
            }

            return null;
        }

        // ============================================================
        // Helper : retourne le TenantId de l'utilisateur connecté
        // ============================================================
        private async Task<Guid?> GetCurrentTenantIdAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return null;

            var tenant = await _context.Tenants
                .Include(t => t.Utilisateur)
                .FirstOrDefaultAsync(t => t.UtilisateurId == userId.Value
                                       && t.Statut == TenantStatus.Approved);

            if (tenant != null)
            {
                return tenant.Id;
            }

            return null;
        }

        // ============================================================
        // GET /api/tenant/companies
        // ============================================================
        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Ok(new { success = false, message = "Compte non approuvé ou token invalide", statut = (string?)null });

            var (success, message, data, statut) = await _tenantService.GetCompaniesAsync(tenantId.Value);

            if (!success)
                return Ok(new { success = false, message, statut });

            return Ok(new { success = true, data, statut });
        }

        // ============================================================
        // GET /api/tenant/companies/{id}
        // ============================================================
        [HttpGet("companies/{id}")]
        public async Task<IActionResult> GetCompany(Guid id)
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            var company = await _tenantService.GetCompanyByIdAsync(id, tenantId.Value);
            if (company == null)
                return NotFound(new { success = false, message = "Entreprise non trouvée" });

            return Ok(new { success = true, data = company });
        }

        // ============================================================
        // POST /api/tenant/companies
        // ============================================================
        [HttpPost("companies")]
        public async Task<IActionResult> CreateCompany([FromForm] CreateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

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

        // ============================================================
        // PUT /api/tenant/companies/{id}
        // ============================================================
        [HttpPut("companies/{id}")]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromForm] UpdateCompanyDto dto, IFormFile? logo = null)
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            var (success, message, data) = await _tenantService.UpdateCompanyAsync(id, tenantId.Value, dto, logo);

            if (!success)
                return NotFound(new { success = false, message });

            return Ok(new { success = true, message, data });
        }

        // ============================================================
        // DELETE /api/tenant/companies/{id}
        // ============================================================
        [HttpDelete("companies/{id}")]
        public async Task<IActionResult> DeleteCompany(Guid id)
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            var (success, message, data) = await _tenantService.DeleteCompanyAsync(id, tenantId.Value);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, data });
        }
        





  // ═════════════════════════════════════════════════════════
// GET /api/tenant/profile
// ═════════════════════════════════════════════════════════
[HttpGet("profile")]
public async Task<IActionResult> GetProfile()
{
    var tenantId = await GetCurrentTenantIdAsync();
    if (tenantId == null)
        return Unauthorized(new { success = false, message = "Non autorisé" });

    var (success, message, data) = await _profileService.GetProfileAsync(tenantId.Value);
    if (!success)
        return NotFound(new { success = false, message });

    return Ok(new { success = true, data });
}

// ═════════════════════════════════════════════════════════
// PUT /api/tenant/profile
// ═════════════════════════════════════════════════════════
[HttpPut("profile")]
public async Task<IActionResult> UpdateProfile(
    [FromForm] TenantProfileDto dto,
    IFormFile? logo)
{
    var tenantId = await GetCurrentTenantIdAsync();
    if (tenantId == null)
        return Unauthorized(new { success = false, message = "Non autorisé" });

    var (success, message, data) = await _profileService.UpdateProfileAsync(tenantId.Value, dto, logo);
    if (!success)
        return BadRequest(new { success = false, message });

    return Ok(new { success = true, message, data });
}




// 2. Remplace la méthode ChangePassword entièrement
[HttpPost("change-password")]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
{
    var userId = GetCurrentUserId();
    if (userId == null)
        return Unauthorized(new { success = false, message = "Non autorisé." });

    if (string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
        string.IsNullOrWhiteSpace(dto.NewPassword)     ||
        string.IsNullOrWhiteSpace(dto.ConfirmPassword))
        return BadRequest(new { success = false, message = "Tous les champs sont obligatoires." });

    if (dto.NewPassword != dto.ConfirmPassword)
        return BadRequest(new { success = false, message = "Les nouveaux mots de passe ne correspondent pas." });

    if (dto.NewPassword.Length < 8)
        return BadRequest(new { success = false, message = "Le mot de passe doit contenir au moins 8 caractères." });

    var utilisateur = await _context.Utilisateurs
        .FirstOrDefaultAsync(u => u.Id == userId.Value);

    if (utilisateur == null)
        return NotFound(new { success = false, message = "Utilisateur introuvable." });

    // ✅ Utilise le même VerifyPassword que AuthService
    if (!_authService.VerifyPassword(dto.CurrentPassword, utilisateur.MotDePasseHash))
        return BadRequest(new { success = false, message = "Mot de passe actuel incorrect." });

    // ✅ Utilise le même HashPassword que AuthService
    utilisateur.MotDePasseHash = _authService.HashPassword(dto.NewPassword);
    await _context.SaveChangesAsync();

    return Ok(new { success = true, message = "Mot de passe mis à jour avec succès." });



}



}}