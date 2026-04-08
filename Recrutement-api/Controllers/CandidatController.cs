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
        private readonly CandidateProfileService _profileService;
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
            CandidateProfileService profileService)  // ← AJOUTÉ pour accès wwwroot
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

        // ═════════════════════════════════════════════════════════
        // GET /api/candidat/profile
        // ═════════════════════════════════════════════════════════
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCandidatIdFromToken();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });
 
            var (success, message, data) = await _profileService.GetProfileAsync(userId.Value);
 
            if (!success)
                return NotFound(new { success = false, message });
 
            return Ok(new { success = true, data });
        }
 
        // ═════════════════════════════════════════════════════════
        // PUT /api/candidat/profile
        // ═════════════════════════════════════════════════════════
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(
            [FromForm] CandidateProfileDto dto,
            IFormFile? avatar)
        {
            var userId = GetCandidatIdFromToken();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });
 
            var (success, message, data) = await _profileService.UpdateProfileAsync(userId.Value, dto, avatar);
 
            if (!success)
                return BadRequest(new { success = false, message });
 
            return Ok(new { success = true, message, data });
        }
        // ─────────────────────────────────────────────────────────────
// ─────────────────────────────────────────────────────────────────────
        // GET /api/candidatures/{id}
        // Retourne le détail d'une candidature pour la vue profil candidat
        // Accessible par les recruteurs (Tenant) et les experts
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                    .ThenInclude(c => c.Utilisateur)
                .FirstOrDefaultAsync(c => c.Id == id);
 
            if (candidature == null)
                return NotFound(new { message = "Candidature introuvable." });
 
            return Ok(new
            {
                candidature.Id,
                candidature.Statut,
                candidature.CvUrl,
                candidature.CreeLe,
                candidature.FormulaireResponses,
 
                // Titre de l'offre liée
                titreOffre = candidature.Offre != null ? candidature.Offre.Titre : null,
 
                // ⚠️ IMPORTANT : on retourne l'UtilisateurId du candidat
                // C'est ce que le frontend utilise pour appeler GET /api/candidat/{candidatId}/profile
                candidatId = candidature.Candidat != null
                                 ? candidature.Candidat.UtilisateurId
                                 : (Guid?)null,
            });
        }
 
        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/candidatures/{id}/statut
        // Met à jour le statut d'une candidature
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/statut")]
        public async Task<IActionResult> UpdateStatut(Guid id, [FromBody] UpdateStatutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Statut))
                return BadRequest(new { message = "Statut invalide." });
 
            var allowed = new[] { "Nouvelle", "En cours", "Acceptée", "Refusée" };
            if (!allowed.Contains(dto.Statut))
                return BadRequest(new { message = $"Statut non reconnu. Valeurs autorisées : {string.Join(", ", allowed)}" });
 
            var candidature = await _context.Candidatures.FindAsync(id);
            if (candidature == null)
                return NotFound(new { message = "Candidature introuvable." });
 
            candidature.Statut = dto.Statut;
            await _context.SaveChangesAsync();
 
            _logger.LogInformation("✅ Statut candidature {id} → {statut}", id, dto.Statut);
            return Ok(new { message = "Statut mis à jour.", statut = dto.Statut });
        }
    }
 
    // DTO pour le PATCH statut
    public class UpdateStatutDto
    {
        public string Statut { get; set; } = "";
    }}