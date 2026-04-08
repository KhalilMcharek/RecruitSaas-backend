using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.Services.CandidatServices;
using Recrutement_api.Services.CvExtraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Recrutement_api.Services.CandidateServices;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CandidatController : ControllerBase
    {
        private readonly ICandidatService _candidatService;
        private readonly ICvExtractionService _cvExtractionService;
        private readonly ILogger<CandidatController> _logger;
         private readonly IOffreService _offreService;
        private readonly IWebHostEnvironment _env;
        private readonly CandidateProfileFileService _profileService;
        // Extensions d'images reconnues par Cloudinary nativement
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "webp", "bmp", "tiff", "tif", "svg", "ico", "avif", "heic", "heif"
        };

        // Extensions qui doivent TOUJOURS être traitées comme raw
        private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "docx", "doc", "odt", "rtf", "txt",
            "zip", "rar", "7z", "tar", "gz",
            "xlsx", "xls", "pptx", "ppt",
            "csv", "json", "xml"
        };

        public CandidatController(
            ICandidatService candidatService,
            ILogger<CandidatController> logger,
             ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            IOffreService offreService,
            IWebHostEnvironment env,
            CandidateProfileFileService profileService),
        {
            _candidatService = candidatService;
            _logger          = logger;
             _context = context;
            _offreService = offreService;
            _env = env; 
            _profileService = profileService; 
            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : extrait le UserId depuis le JWT
        // ─────────────────────────────────────────────────────────────
        private Guid? GetUserIdFromToken()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("candidatId")
                     ?? User.FindFirstValue("nameid")
                     ?? User.FindFirstValue("sub");

            if (claim == null) return null;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/candidat/formulaire/{offreId}
        // ─────────────────────────────────────────────────────────────
        private static bool MustUploadAsRaw(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName)?.TrimStart('.') ?? string.Empty;

            if (RawExtensions.Contains(ext)) return true;
            if (ImageExtensions.Contains(ext)) return false;

            if (!string.IsNullOrEmpty(file.ContentType) &&
                file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : upload un fichier vers Cloudinary
        // ─────────────────────────────────────────────────────────────
        private async Task<(string? Url, string? Error)> UploadToCloudinary(
            IFormFile file, string folder)
        {
            using var stream = file.OpenReadStream();

            if (MustUploadAsRaw(file))
            {
                _logger.LogInformation("Cloudinary upload RAW: {name}", file.FileName);

                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    Type = "upload",
                    AccessMode = "public"
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    _logger.LogWarning("Cloudinary RAW error: {msg}", result.Error.Message);
                    return (null, result.Error.Message);
                }

                return (result.SecureUrl.ToString(), null);
            }
            else
            {
                _logger.LogInformation("Cloudinary upload IMAGE: {name}", file.FileName);

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    Transformation = new Transformation().Quality("auto")
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    _logger.LogWarning("Cloudinary IMAGE error: {msg}", result.Error.Message);
                    return (null, result.Error.Message);
                }

                return (result.SecureUrl.ToString(), null);
            }
        }

        // ═════════════════════════════════════════════════════════════
        // EXISTING ENDPOINTS (inchangés)
        // ═════════════════════════════════════════════════════════════

        [HttpGet("formulaire/{offreId}")]
        public async Task<IActionResult> GetFormulaire(Guid offreId)
        {
            var result = await _candidatService.GetFormulaireAsync(offreId);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/candidat/postuler
        // ─────────────────────────────────────────────────────────────
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler([FromForm] PostulerDto dto)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var result = await _candidatService.PostulerAsync(userId.Value, dto);

            // test extraction CV
            try
            {
                await _cvExtractionService.ProcessCvAsync(result.CandidatureId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'extraction de CV (test).");
            }

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/candidat/mes-candidatures
        // ─────────────────────────────────────────────────────────────
        [HttpGet("mes-candidatures")]
        public async Task<IActionResult> MesCandidatures()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidatures = await _candidatService.GetMesCandidaturesAsync(userId.Value);
            return Ok(candidatures);
        }

        // ═════════════════════════════════════════════════════════════
        // OFFRES — PUBLIC (AllowAnonymous)
        // ═════════════════════════════════════════════════════════════

        [HttpGet("offres")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffres()
        {
            var offres = await _candidatService.GetOffresAsync();
            return Ok(offres);
        }

        [HttpGet("offres/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffreDetail(Guid id)
        {
            var offre = await _candidatService.GetOffreDetailAsync(id);

            if (offre == null)
                return NotFound(new { message = "Offre introuvable ou non publiée." });

            return Ok(offre);
        }

 // ═══════════════════════════════════════════════════════════════════════
// CORRECTIONS BACKEND - CandidatController
// ═══════════════════════════════════════════════════════════════════════
 
// 🔴 AJOUTER CET ENDPOINT (MANQUAIT) :
[HttpGet("profile")]
public async Task<IActionResult> GetProfile()
{
    var candidatId = GetCandidatIdFromToken();
    if (candidatId == null)
        return Unauthorized(new { success = false, message = "Non autorisé" });
 
    try
    {
        var profileFolder = Path.Combine(_env.WebRootPath, "data", "candidate-profiles");
        var jsonPath = Path.Combine(profileFolder, $"{candidatId}.json");
 
        if (!System.IO.File.Exists(jsonPath))
        {
            // Retourner un profil vide plutôt que 404
            return Ok(new
            {
                success = true,
                data = new
                {
                    fullName = "",
                    email = "",
                    phone = "",
                    location = "",
                    bio = "",
                    seeking = "student",
                    education = "",
                    fieldOfStudy = "",
                    experience = "",
                    availability = "",
                    skills = new List<string>(),
                    linkedin = "",
                    github = "",
                    portfolioUrl = "",
                    avatarUrl = (string?)null
                }
            });
        }
 
        var json = await System.IO.File.ReadAllTextAsync(jsonPath);
        var profileData = JsonSerializer.Deserialize<dynamic>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
 
        return Ok(new { success = true, data = profileData });
    }
    catch (Exception ex)
    {
        _logger.LogError("❌ GetProfile error: {msg}", ex.Message);
        return StatusCode(500, new { success = false, message = "Erreur serveur", error = ex.Message });
    }
}
 
// ═══════════════════════════════════════════════════════════════════════
 
// 🟡 CORRIGER L'ENDPOINT PUT /profile EXISTANT:
[HttpPut("profile")]
public async Task<IActionResult> UpdateProfile([FromForm] UpdateCandidateProfileDto dto, IFormFile? avatar)
{
    var candidatId = GetCandidatIdFromToken();
    if (candidatId == null)
        return Unauthorized(new { success = false, message = "Non autorisé" });
 
    try
    {
        var profileFolder = Path.Combine(_env.WebRootPath, "data", "candidate-profiles");
        var imagesFolder = Path.Combine(_env.WebRootPath, "imagesCandidate");
        var jsonPath = Path.Combine(profileFolder, $"{candidatId}.json");
 
        // Lire l'existant
        string? existingAvatarUrl = null;
        if (System.IO.File.Exists(jsonPath))
        {
            try
            {
                var raw = await System.IO.File.ReadAllTextAsync(jsonPath);
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("avatarUrl", out var av) && av.ValueKind != JsonValueKind.Null)
                    existingAvatarUrl = av.GetString();
            }
            catch { /* Ignorer erreurs de parsing */ }
        }
 
        // ─────────────────────────────────────────────
        // Gestion avatar
        // ─────────────────────────────────────────────
        string? avatarUrl = existingAvatarUrl; // 👈 IMPORTANT: garder l'ancien par défaut
 
        if (avatar != null && avatar.Length > 0)
        {
            if (avatar.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "L'avatar ne doit pas dépasser 5 Mo" });
 
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            if (!allowed.Contains(ext))
                return BadRequest(new { success = false, message = "Format non supporté" });
 
            if (!Directory.Exists(imagesFolder))
                Directory.CreateDirectory(imagesFolder);
 
            // Supprimer ancien avatar uniquement s'il y en a un
            if (!string.IsNullOrEmpty(existingAvatarUrl) && existingAvatarUrl.Contains("/imagesCandidate/"))
            {
                try
                {
                    var oldFile = Path.GetFileName(new Uri(existingAvatarUrl).LocalPath);
                    var oldPath = Path.Combine(imagesFolder, oldFile);
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                        _logger.LogInformation("✅ Ancien avatar supprimé: {file}", oldFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Erreur suppression ancien avatar: {msg}", ex.Message);
                }
            }
 
            // Sauvegarder nouveau fichier
            var fileName = $"{candidatId}_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(imagesFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await avatar.CopyToAsync(stream);
 
            // URL relative pour le frontend (IMPORTANT: doit être accessible par UseStaticFiles)
            avatarUrl = $"/imagesCandidate/{fileName}";
            _logger.LogInformation("✅ Nouvel avatar sauvegardé: {url}", avatarUrl);
        }
 
        // ─────────────────────────────────────────────
        // Construire le profil complet
        // ─────────────────────────────────────────────
        var profileData = new
        {
            fullName = dto.FullName ?? "",
            email = dto.Email ?? "",
            phone = dto.Phone ?? "",
            location = dto.Location ?? "",
            bio = dto.Bio ?? "",
            seeking = dto.Seeking ?? "student",
            education = dto.Education ?? "",
            fieldOfStudy = dto.FieldOfStudy ?? "",
            experience = dto.Experience ?? "",
            availability = dto.Availability ?? "",
            skills = dto.Skills ?? new List<string>(),
            linkedin = dto.Linkedin ?? "",
            github = dto.Github ?? "",
            portfolioUrl = dto.PortfolioUrl ?? "",
            avatarUrl = avatarUrl,  // 👈 CRUCIAL: toujours inclure
            updatedAt = DateTime.UtcNow
        };
 
        // ─────────────────────────────────────────────
        // Sauvegarder JSON
        // ─────────────────────────────────────────────
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
        _logger.LogInformation("✅ Profil sauvegardé: {id}", candidatId);
 
        return Ok(new { success = true, message = "Profil enregistré", data = profileData });
    }
    catch (Exception ex)
    {
        _logger.LogError("❌ UpdateProfile error: {msg}", ex.Message);
        return StatusCode(500, new { success = false, message = "Erreur serveur", error = ex.Message });
    }
}
    }}
