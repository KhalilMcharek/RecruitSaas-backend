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

namespace Recrutement_api.Controllers
{
    [Authorize]
    [Route("api/tenant")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly TenantService _tenantService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env; // ← AJOUTÉ

        public TenantController(TenantService tenantService, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _tenantService = tenantService;
            _context = context;
            _env = env;
            
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
        





  // ═══════════════════════════════════════════════════════════
        // ⭐ GET /api/tenant/profile - Lecture du profil tenant
        // ═══════════════════════════════════════════════════════════
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            // Chemin vers le fichier JSON dans wwwroot/data/tenant-profiles/
            var profileFolder = Path.Combine(_env.WebRootPath, "data", "tenant-profiles");
            var jsonPath = Path.Combine(profileFolder, $"{tenantId}.json");

            // Si le fichier n'existe pas, retourner un profil vide par défaut
            if (!System.IO.File.Exists(jsonPath))
            {
                return Ok(new 
                { 
                    success = true, 
                    data = new 
                    {
                        WorkTypes = new List<string>(),
                        TechStack = new List<string>(),
                        HiringStatus = "actively"
                    } 
                });
            }

            try
            {
                var raw = await System.IO.File.ReadAllTextAsync(jsonPath);
                
                // Options de désérialisation
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var data = JsonSerializer.Deserialize<JsonElement>(raw, options);
                
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                // Logger l'erreur en production
                return StatusCode(500, new { success = false, message = "Erreur de lecture du profil", error = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // ⭐ PUT /api/tenant/profile - Mise à jour du profil tenant
        // ═══════════════════════════════════════════════════════════
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(
            [FromForm] UpdateTenantProfileDto dto,
            IFormFile? logo)
        {
            var tenantId = await GetCurrentTenantIdAsync();
            if (tenantId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            try
            {
                // ── 1. Définir les chemins avec WebRootPath ──
                var profileFolder = Path.Combine(_env.WebRootPath, "data", "tenant-profiles");
                var imagesFolder = Path.Combine(_env.WebRootPath, "imagesProfile");
                var jsonPath = Path.Combine(profileFolder, $"{tenantId}.json");

                // ── 2. Lire le profil existant pour préserver certaines données ──
                string? existingLogoUrl = null;
                int existingActiveJobs = 0;
                int existingTotalHires = 0;

                if (System.IO.File.Exists(jsonPath))
                {
                    var raw = await System.IO.File.ReadAllTextAsync(jsonPath);
                    var existing = JsonDocument.Parse(raw).RootElement;

                    if (existing.TryGetProperty("logoUrl", out var lu) && lu.ValueKind != JsonValueKind.Null)
                        existingLogoUrl = lu.GetString();
                    else if (existing.TryGetProperty("LogoUrl", out var lu2) && lu2.ValueKind != JsonValueKind.Null)
                        existingLogoUrl = lu2.GetString();

                    if (existing.TryGetProperty("activeJobs", out var aj))
                        existingActiveJobs = aj.GetInt32();
                    else if (existing.TryGetProperty("ActiveJobs", out var aj2))
                        existingActiveJobs = aj2.GetInt32();

                    if (existing.TryGetProperty("totalHires", out var th))
                        existingTotalHires = th.GetInt32();
                    else if (existing.TryGetProperty("TotalHires", out var th2))
                        existingTotalHires = th2.GetInt32();
                }

                // ── 3. Gestion de l'upload du logo ──
                string? logoUrl = existingLogoUrl;

                if (logo != null && logo.Length > 0)
                {
                    // Validation : taille max 5MB
                    if (logo.Length > 5 * 1024 * 1024)
                        return BadRequest(new { success = false, message = "Le logo ne doit pas dépasser 5 Mo" });

                    // Validation : extensions autorisées
                    var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                    var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
                    
                    if (!allowedExts.Contains(ext))
                        return BadRequest(new { success = false, message = "Format d'image non supporté (jpg, png, webp, svg)" });

                    // Créer le dossier si nécessaire
                    if (!Directory.Exists(imagesFolder))
                        Directory.CreateDirectory(imagesFolder);

                    // Supprimer l'ancien logo local si présent
                    if (!string.IsNullOrEmpty(existingLogoUrl) && 
                        existingLogoUrl.Contains("/imagesProfile/"))
                    {
                        try
                        {
                            var oldFileName = Path.GetFileName(new Uri(existingLogoUrl).LocalPath);
                            var oldPath = Path.Combine(imagesFolder, oldFileName);
                            if (System.IO.File.Exists(oldPath))
                                System.IO.File.Delete(oldPath);
                        }
                        catch { /* Ignorer les erreurs de suppression */ }
                    }

                    // Sauvegarder le nouveau fichier
                    var fileName = $"{tenantId}_{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(imagesFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await logo.CopyToAsync(stream);

                    // URL relative pour le frontend (compatible avec les fichiers statiques)
                    logoUrl = $"/imagesProfile/{fileName}";
                }

                // ── 4. Construire l'objet profil avec sérialisation camelCase ──
                var profileData = new
                {
                    companyName = dto.CompanyName,
                    industry = dto.Industry,
                    companySize = dto.CompanySize,
                    location = dto.Location,
                    description = dto.Description,
                    website = dto.Website,
                    linkedin = dto.Linkedin,
                    twitter = dto.Twitter,
                    hiringStatus = dto.HiringStatus ?? "actively",
                    fullName = dto.FullName,
                    email = dto.Email,
                    jobTitle = dto.JobTitle,
                    phone = dto.Phone,
                    workTypes = dto.WorkTypes ?? new List<string>(),
                    techStack = dto.TechStack ?? new List<string>(),
                    logoUrl = logoUrl,
                    activeJobs = existingActiveJobs,
                    totalHires = existingTotalHires,
                    updatedAt = DateTime.UtcNow
                };

                // ── 5. Sauvegarder dans le fichier JSON ──
                if (!Directory.Exists(profileFolder))
                    Directory.CreateDirectory(profileFolder);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(profileData, jsonOptions);
                await System.IO.File.WriteAllTextAsync(jsonPath, json);

                // ── 6. Retourner la réponse ──
                return Ok(new { success = true, message = "Profil enregistré avec succès", data = profileData });
            }
            catch (Exception ex)
            {
                // Logger l'erreur en production
                return StatusCode(500, new { success = false, message = "Erreur serveur lors de la sauvegarde", error = ex.Message });
            }
        }
    }}