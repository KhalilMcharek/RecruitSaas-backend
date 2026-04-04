using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;
using System.Security.Claims;

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

        // Extensions d'images reconnues par Cloudinary nativement
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "webp", "bmp", "tiff", "tif", "svg", "ico", "avif", "heic", "heif"
        };

        // Extensions qui doivent TOUJOURS être traitées comme raw (jamais comme image)
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
            IOffreService offreService)
        {
            _context = context;
            _logger = logger;
            _offreService = offreService;

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
            if (Guid.TryParse(claim, out var id)) return id;
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : détermine si le fichier doit être uploadé en raw
        // Priorité à l'extension — un PDF ne doit JAMAIS passer en image
        // ─────────────────────────────────────────────────────────────
        private static bool MustUploadAsRaw(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName)?.TrimStart('.') ?? string.Empty;

            // 1. Si l'extension est dans la liste raw → toujours raw
            if (RawExtensions.Contains(ext))
                return true;

            // 2. Si l'extension est une image connue → pas raw
            if (ImageExtensions.Contains(ext))
                return false;

            // 3. Fallback : vérifier le Content-Type
            if (!string.IsNullOrEmpty(file.ContentType) &&
                file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return false;

            // 4. Inconnu → raw par sécurité
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : upload un fichier vers Cloudinary
        //
        // RÈGLE STRICTE :
        //   - PDF, DOCX, etc.  → RawUploadParams  (resource_type=raw)
        //   - Images           → ImageUploadParams (resource_type=image)
        //   - NE PAS utiliser FetchFormat("auto") → modifie l'extension
        //     et corrompait les URLs stockées en base
        // ─────────────────────────────────────────────────────────────
        private async Task<(string? Url, string? Error)> UploadToCloudinary(
            IFormFile file, string folder)
        {
            using var stream = file.OpenReadStream();

            if (MustUploadAsRaw(file))
            {
                // ── Fichier brut (PDF, DOCX, ZIP…) ────────────────
                _logger.LogInformation("Cloudinary upload RAW: {name}", file.FileName);

                var uploadParams = new RawUploadParams
                {
                    File       = new FileDescription(file.FileName, stream),
                    Folder     = folder,
                    Type       = "upload",
                    AccessMode = "public"
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    _logger.LogWarning("Cloudinary RAW error: {msg}", result.Error.Message);
                    return (null, result.Error.Message);
                }

                _logger.LogInformation("Cloudinary RAW OK → {url}", result.SecureUrl);
                return (result.SecureUrl.ToString(), null);
            }
            else
            {
                // ── Image ──────────────────────────────────────────
                _logger.LogInformation("Cloudinary upload IMAGE: {name}", file.FileName);

                var uploadParams = new ImageUploadParams
                {
                    File   = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    // ⚠️ Quality("auto") uniquement — PAS de FetchFormat("auto")
                    //    FetchFormat change l'extension dans l'URL retournée
                    //    ce qui corrompait les URLs stockées en base
                    Transformation = new Transformation().Quality("auto")
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    _logger.LogWarning("Cloudinary IMAGE error: {msg}", result.Error.Message);
                    return (null, result.Error.Message);
                }

                _logger.LogInformation("Cloudinary IMAGE OK → {url}", result.SecureUrl);
                return (result.SecureUrl.ToString(), null);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // ✅ GET api/candidat/formulaire/{offreId}
        // ─────────────────────────────────────────────────────────────
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
                id      = formulaire.Id,
                offreId = formulaire.OffreId,
                champsPersonnalises = formulaire.ChampsPersonnalises
                    .OrderBy(c => c.Ordre)
                    .Select(c => new
                    {
                        id             = c.Id,
                        nom            = c.Nom,
                        question       = c.Question,
                        type           = c.Type,
                        estObligatoire = c.EstObligatoire,
                        optionsJson    = c.OptionsJson,
                        ordre          = c.Ordre
                    })
                    .ToList()
            });
        }

        // ─────────────────────────────────────────────────────────────
        // ✅ POST api/candidat/postuler
        // Accepte : PDF, DOCX, DOC, PNG, JPG, JPEG, GIF, WEBP, BMP,
        //           TIFF, SVG, ZIP, RAR, et tout autre format
        // ─────────────────────────────────────────────────────────────
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler(
            [FromForm] Guid      offreId,
            [FromForm] IFormFile cv,
            [FromForm] string?   fullName,
            [FromForm] string?   email,
            [FromForm] string?   portfolioUrl,
            [FromForm] string?   motivation,
            [FromForm] string?   champsPersonnalises)
        {
            try
            {
                // ─── 1. UserId depuis le JWT ───────────────────────
                var userId = GetCandidatIdFromToken();
                if (userId == null)
                    return Unauthorized(new { message = "Session invalide." });

                // ─── 2. Lookup du candidat ─────────────────────────
                var candidat = await _context.Candidats
                    .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

                if (candidat == null)
                    return NotFound(new { message = "Profil candidat introuvable." });

                if (cv == null || cv.Length == 0)
                    return BadRequest(new { message = "Le CV est obligatoire." });

                // ─── Vérification taille CV (max 10 MB) ───────────
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

                // ─── 3. Upload CV sur Cloudinary ───────────────────
                // MustUploadAsRaw() garantit que les PDFs vont en /raw/upload/
                var (cvUrl, cvError) = await UploadToCloudinary(cv, "cvs");

                if (cvError != null)
                    return StatusCode(500, new { message = $"Erreur upload CV : {cvError}" });

                // ─── 4. Upload fichiers custom sur Cloudinary ──────
                var customFileUrls = new Dictionary<string, string>(); // question → url

                int idx = 0;
                while (true)
                {
                    var fileKey      = $"customFile_{idx}";
                    var questionKey  = $"customFile_{idx}_question";

                    var customFile    = Request.Form.Files.GetFile(fileKey);
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

                    if (fileError != null)
                    {
                        _logger.LogWarning("Cloudinary custom file error: {msg}", fileError);
                    }
                    else
                    {
                        var label = !string.IsNullOrEmpty(customQuestion)
                                        ? customQuestion
                                        : fileKey;
                        customFileUrls[label] = fileUrl!;
                    }

                    idx++;
                }

                // ─── 5. Construire le JSON des réponses ───────────
                var responses = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(fullName))     responses["fullName"]     = fullName;
                if (!string.IsNullOrEmpty(email))        responses["email"]        = email;
                if (!string.IsNullOrEmpty(portfolioUrl)) responses["portfolioUrl"] = portfolioUrl;
                if (!string.IsNullOrEmpty(motivation))   responses["motivation"]   = motivation;

                if (!string.IsNullOrWhiteSpace(champsPersonnalises))
                {
                    try
                    {
                        var extra = System.Text.Json.JsonSerializer
                            .Deserialize<Dictionary<string, object>>(champsPersonnalises);
                        if (extra != null)
                            foreach (var kv in extra)
                                responses[kv.Key] = kv.Value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("JSON invalide champsPersonnalises: {msg}", ex.Message);
                    }
                }

                foreach (var kv in customFileUrls)
                    responses[kv.Key] = kv.Value;

                var formulaireResponsesJson = responses.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(responses)
                    : "{}";

                // ─── 6. INSERT en base ─────────────────────────────
                var candidature = new Candidature
                {
                    Id                  = Guid.NewGuid(),
                    OffreId             = offreId,
                    CandidatId          = candidat.Id,
                    CvUrl               = cvUrl!,
                    Statut              = "Nouvelle",
                    CreeLe              = DateTime.UtcNow,
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

        // ─────────────────────────────────────────────────────────────
        // GET api/candidat/mes-candidatures
        // ─────────────────────────────────────────────────────────────
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
                    Offre = new
                    {
                        c.Offre.Id,
                        c.Offre.Titre,
                        c.Offre.Localisation
                    }
                })
                .ToListAsync();

            return Ok(candidatures);
        }

        // ═════════════════════════════════════════════════════════════
        // OFFRES — PUBLIC (AllowAnonymous)
        // ═════════════════════════════════════════════════════════════

        // GET api/candidat/offres
        [HttpGet("offres")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffres()
        {
            var offres = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.EstPublie)
                .Select(o => new OffreListDTO
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .OrderByDescending(o => o.CreeLe)
                .ToListAsync();

            return Ok(offres);
        }

        // GET api/candidat/offres/{id}
        [HttpGet("offres/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffreDetail(Guid id)
        {
            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.Id == id && o.EstPublie)
                .Select(o => new OffreDetailDTO
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Description   = o.Description,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .FirstOrDefaultAsync();

            if (offre == null)
                return NotFound(new { message = "Offre introuvable ou non publiée." });

            return Ok(offre);
        }
    }
}