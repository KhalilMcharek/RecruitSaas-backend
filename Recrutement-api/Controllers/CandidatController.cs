using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Hangfire;
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
        // POST api/candidat/postuler
        // ─────────────────────────────────────────────────────────────
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
        // ─── 1. UserId depuis le JWT ───
        var userId = GetCandidatIdFromToken();
        if (userId == null)
            return Unauthorized(new { message = "Session invalide." });

        // ─── 2. Lookup via UtilisateurId ───
        var candidat = await _context.Candidats
            .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

        if (candidat == null)
            return NotFound(new { message = "Profil candidat introuvable." });

        if (cv == null || cv.Length == 0)
            return BadRequest(new { message = "Le CV est obligatoire." });

        var offre = await _context.OffresEmploi
            .FirstOrDefaultAsync(o => o.Id == offreId && o.EstPublie);

        if (offre == null)
            return BadRequest(new { message = "Offre invalide." });

        var dejaPostule = await _context.Candidatures
            .AnyAsync(c => c.OffreId == offreId && c.CandidatId == candidat.Id);

        if (dejaPostule)
            return Conflict(new { message = "Déjà postulé." });

        // ─── 3. Upload Cloudinary (cvUrl déclaré AVANT le using) ───
        string cvUrl;
        using (var stream = cv.OpenReadStream())
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(cv.FileName, stream),
                Folder = "cvs"
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                return StatusCode(500, new { message = result.Error.Message });

            cvUrl = result.SecureUrl.ToString();
        }

        // ─── 4. Construire JSON des réponses ───
        var responses = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(fullName))       responses["fullName"]      = fullName;
        if (!string.IsNullOrEmpty(email))          responses["email"]         = email;
        if (!string.IsNullOrEmpty(portfolioUrl))   responses["portfolioUrl"]  = portfolioUrl;
        if (!string.IsNullOrEmpty(motivation))     responses["motivation"]    = motivation;

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
                _logger.LogWarning("JSON invalide: {msg}", ex.Message);
            }
        }

        var formulaireResponsesJson = responses.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(responses)
            : "{}";

        // ─── 5. INSERT DB avec candidat.Id (FK valide) ───
        var candidature = new Candidature
        {
            Id                  = Guid.NewGuid(),
            OffreId             = offreId,
            CandidatId          = candidat.Id,        // ✅ FK réelle
            CvUrl               = cvUrl,              // ✅ dans scope
            Statut              = "Nouvelle",
            CreeLe              = DateTime.UtcNow,
            FormulaireResponses = formulaireResponsesJson  // ✅ dans scope
        };

        _context.Candidatures.Add(candidature);
        await _context.SaveChangesAsync();

        // Ajout à la file d'attente Hangfire pour l'extraction du texte du CV
        BackgroundJob.Enqueue<Recrutement_api.Services.CvExtraction.CvBackgroundProcessingService>(
            service => service.ProcessCvTaskAsync(candidature.Id, candidature.CvUrl));

        return Ok(new { message = "Candidature envoyée", candidature.Id, cvUrl });
    }
    catch (Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        _logger.LogError("Erreur: {msg}", msg);
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

    }}