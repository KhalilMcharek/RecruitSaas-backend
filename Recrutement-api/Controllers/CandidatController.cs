using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
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
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            ILogger<CandidatController> logger,
            IOffreService offreService,
            IWebHostEnvironment env,
            CandidateProfileFileService profileService)  // ← AJOUTÉ pour accès wwwroot
        {
            _context = context;
            _logger = logger;
            _offreService = offreService;
            _env = env; 
            _profileService = profileService; // ← AJOUTÉ

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : extrait le CandidatId depuis le JWT
        // ─────────────────────────────────────────────────────────────
        private Guid? GetCandidatIdFromToken()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("candidatId")
                     ?? User.FindFirstValue("nameid")
                     ?? User.FindFirstValue("sub");

            if (claim == null) return null;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : détermine si le fichier doit être uploadé en raw
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
            var formulaire = await _context.FormulairesCandidature
                .Include(f => f.ChampsPersonnalises)
                .FirstOrDefaultAsync(f => f.OffreId == offreId);

            if (formulaire == null)
                return Ok(new { champsPersonnalises = new List<object>() });

            return Ok(new
            {
                id = formulaire.Id,
                offreId = formulaire.OffreId,
                champsPersonnalises = formulaire.ChampsPersonnalises
                    .OrderBy(c => c.Ordre)
                    .Select(c => new
                    {
                        id = c.Id,
                        nom = c.Nom,
                        question = c.Question,
                        type = c.Type,
                        estObligatoire = c.EstObligatoire,
                        optionsJson = c.OptionsJson,
                        ordre = c.Ordre
                    })
                    .ToList()
            });
        }

        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler(
            [FromForm] Guid offreId,
            [FromForm] IFormFile cv,
            [FromForm] string? fullName,
            [FromForm] string? email,
            [FromForm] string? portfolioUrl,
            [FromForm] string? motivation,
            [FromForm] string? champsPersonnalises)
        {
            try
            {
                var userId = GetCandidatIdFromToken();
                if (userId == null)
                    return Unauthorized(new { message = "Session invalide." });

                var candidat = await _context.Candidats
                    .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

                if (candidat == null)
                    return NotFound(new { message = "Profil candidat introuvable." });

                if (cv == null || cv.Length == 0)
                    return BadRequest(new { message = "Le CV est obligatoire." });

                if (cv.Length > 10 * 1024 * 1024)
                    return BadRequest(new { message = "Le CV ne doit pas dépasser 10MB." });

                var offre = await _context.OffresEmploi
                    .FirstOrDefaultAsync(o => o.Id == offreId && o.EstPublie);

                if (offre == null)
                    return BadRequest(new { message = "Offre invalide." });

                var dejaPostule = await _context.Candidatures
                    .AnyAsync(c => c.OffreId == offreId && c.CandidatId == candidat.Id);

                if (dejaPostule)
                    return Conflict(new { message = "Déjà postulé." });

                var (cvUrl, cvError) = await UploadToCloudinary(cv, "cvs");
                if (cvError != null)
                    return StatusCode(500, new { message = $"Erreur upload CV : {cvError}" });

                var customFileUrls = new Dictionary<string, string>();
                int idx = 0;
                while (true)
                {
                    var fileKey = $"customFile_{idx}";
                    var questionKey = $"customFile_{idx}_question";

                    var customFile = Request.Form.Files.GetFile(fileKey);
                    var customQuestion = Request.Form.ContainsKey(questionKey)
                                            ? Request.Form[questionKey].ToString()
                                            : null;

                    if (customFile == null) break;

                    if (customFile.Length > 10 * 1024 * 1024)
                    {
                        _logger.LogWarning("Fichier custom trop lourd: {name}", customFile.FileName);
                        idx++;
                        continue;
                    }

                    var (fileUrl, fileError) = await UploadToCloudinary(customFile, "candidatures_fichiers");
                    if (fileError == null)
                    {
                        var label = !string.IsNullOrEmpty(customQuestion) ? customQuestion : fileKey;
                        customFileUrls[label] = fileUrl!;
                    }
                    idx++;
                }

                var responses = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(fullName)) responses["fullName"] = fullName;
                if (!string.IsNullOrEmpty(email)) responses["email"] = email;
                if (!string.IsNullOrEmpty(portfolioUrl)) responses["portfolioUrl"] = portfolioUrl;
                if (!string.IsNullOrEmpty(motivation)) responses["motivation"] = motivation;

                if (!string.IsNullOrWhiteSpace(champsPersonnalises))
                {
                    try
                    {
                        var extra = JsonSerializer.Deserialize<Dictionary<string, object>>(champsPersonnalises);
                        if (extra != null)
                            foreach (var kv in extra) responses[kv.Key] = kv.Value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("JSON invalide champsPersonnalises: {msg}", ex.Message);
                    }
                }

                foreach (var kv in customFileUrls) responses[kv.Key] = kv.Value;

                var formulaireResponsesJson = responses.Count > 0
                    ? JsonSerializer.Serialize(responses)
                    : "{}";

                var candidature = new Candidature
                {
                    Id = Guid.NewGuid(),
                    OffreId = offreId,
                    CandidatId = candidat.Id,
                    CvUrl = cvUrl!,
                    Statut = "Nouvelle",
                    CreeLe = DateTime.UtcNow,
                    FormulaireResponses = formulaireResponsesJson
                };

                _context.Candidatures.Add(candidature);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Candidature créée: {id}", candidature.Id);
                return Ok(new { message = "Candidature envoyée", candidature.Id, cvUrl });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError("❌ Erreur postuler: {msg}", msg);
                return StatusCode(500, new { message = msg });
            }
        }

        [HttpGet("mes-candidatures")]
        public async Task<IActionResult> MesCandidatures()
        {
            var candidatId = GetCandidatIdFromToken();
            if (candidatId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidatures = await _context.Candidatures
                .Where(c => c.CandidatId == candidatId.Value)
                .Include(c => c.Offre)
                .OrderByDescending(c => c.CreeLe)
                .Select(c => new
                {
                    c.Id,
                    c.Statut,
                    c.CvUrl,
                    c.CreeLe,
                    Offre = new { c.Offre.Id, c.Offre.Titre, c.Offre.Localisation }
                })
                .ToListAsync();

            return Ok(candidatures);
        }

        [HttpGet("offres")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffres()
        {
            var offres = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.EstPublie)
                .Select(o => new OffreListDTO
                {
                    Id = o.Id,
                    Titre = o.Titre,
                    Localisation = o.Localisation,
                    CreeLe = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .OrderByDescending(o => o.CreeLe)
                .ToListAsync();

            return Ok(offres);
        }

        [HttpGet("offres/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffreDetail(Guid id)
        {
            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.Id == id && o.EstPublie)
                .Select(o => new OffreDetailDTO
                {
                    Id = o.Id,
                    Titre = o.Titre,
                    Description = o.Description,
                    Localisation = o.Localisation,
                    CreeLe = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .FirstOrDefaultAsync();

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