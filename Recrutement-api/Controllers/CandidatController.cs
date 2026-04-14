using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Models;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.CandidatServices;
using Recrutement_api.Services.CvExtraction;
using Recrutement_api.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly CandidateProfileService _profileService;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "webp", "bmp",
            "tiff", "tif", "svg", "ico", "avif", "heic", "heif"
        };

        private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "docx", "doc", "odt", "rtf", "txt",
            "zip", "rar", "7z", "tar", "gz",
            "xlsx", "xls", "pptx", "ppt",
            "csv", "json", "xml"
        };

        public CandidatController(
            ICandidatService candidatService,
            ICvExtractionService cvExtractionService,
            ILogger<CandidatController> logger,
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            IOffreService offreService,
            IWebHostEnvironment env,
            CandidateProfileService profileService)
        {
            _candidatService     = candidatService;
            _cvExtractionService = cvExtractionService;
            _logger              = logger;
            _context             = context;
            _offreService        = offreService;
            _env                 = env;
            _profileService      = profileService;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────
        // Helper : extract UserId from JWT
        // ─────────────────────────────────────────────
        private Guid? GetUserIdFromToken()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("candidatId")
                     ?? User.FindFirstValue("nameid")
                     ?? User.FindFirstValue("sub");

            if (claim == null) return null;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────
        // GET FORMULAIRE
        // ─────────────────────────────────────────────
        [HttpGet("formulaire/{offreId}")]
        public async Task<IActionResult> GetFormulaire(Guid offreId)
        {
            var result = await _candidatService.GetFormulaireAsync(offreId);
            return Ok(result);
        }

        // ─────────────────────────────────────────────
        // POSTULER
        // ✅ AlreadyAppliedException  → 409 Conflict  (frontend affiche le banner)
        // ✅ Autres Exception         → 400 BadRequest (message inline)
        // ─────────────────────────────────────────────
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler([FromForm] PostulerDto dto)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            try
            {
                var result = await _candidatService.PostulerAsync(userId.Value, dto);
                return Ok(result);
            }
            catch (AlreadyAppliedException)
            {
                // ✅ 409 → frontend détecte e.response.status === 409
                //          et affiche le banner "Already Applied"
                return Conflict(new { message = "Vous avez déjà postulé à cette offre." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la candidature userId={UserId}", userId);
                return BadRequest(new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────
        // MES CANDIDATURES
        // ─────────────────────────────────────────────
        [HttpGet("mes-candidatures")]
        public async Task<IActionResult> MesCandidatures()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var result = await _candidatService.GetMesCandidaturesAsync(userId.Value);
            return Ok(result);
        }

        // ─────────────────────────────────────────────
        // OFFRES PUBLIC
        // ─────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("offres")]
        public async Task<IActionResult> GetOffres()
        {
            var offres = await _candidatService.GetOffresAsync();
            return Ok(offres);
        }

        [AllowAnonymous]
        [HttpGet("offres/{id:guid}")]
        public async Task<IActionResult> GetOffreDetail(Guid id)
        {
            var offre = await _candidatService.GetOffreDetailAsync(id);
            if (offre == null)
                return NotFound(new { message = "Offre introuvable" });
            return Ok(offre);
        }

        // ═════════════════════════════════════════════════════════
        // GET /api/candidat/profile  (candidat connecté)
        // ═════════════════════════════════════════════════════════
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            var (success, message, data) = await _profileService.GetProfileAsync(userId.Value);

            if (!success)
                return Ok(new { success = false, message });

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
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });

            var (success, message, data) = await _profileService.UpdateProfileAsync(userId.Value, dto, avatar);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, data });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/candidat/{id}
        // Détail d'une candidature (recruteur / expert)
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
                titreOffre = candidature.Offre != null ? candidature.Offre.Titre : null,
                // ⚠️ UtilisateurId du candidat → utilisé par le frontend pour GET /api/candidat/{userId}/profile
                candidatId = candidature.Candidat != null
                                 ? candidature.Candidat.UtilisateurId
                                 : (Guid?)null,
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/candidat/{id}/statut
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

            _logger.LogInformation("✅ Statut candidature {Id} → {Statut}", id, dto.Statut);
            return Ok(new { message = "Statut mis à jour.", statut = dto.Statut });
        }

        // ═══════════════════════════════════════════════════════
        // GET /api/candidat/{userId}/profile
        // Profil public d'un candidat (recruteur/expert)
        // ═══════════════════════════════════════════════════════
        [HttpGet("{userId:guid}/profile")]
        public async Task<IActionResult> GetCandidateProfileById(Guid userId)
        {
            var (success, message, data) = await _profileService.GetProfileAsync(userId);

            if (!success)
                return Ok(new { success = false, message });

            return Ok(new { success = true, data });
        }
        // DELETE /api/candidat/postuler/{offreId}
[HttpDelete("postuler/{offreId:guid}")]
public async Task<IActionResult> CancelCandidature(Guid offreId)
{
    var userId = GetUserIdFromToken();
    if (userId == null)
        return Unauthorized(new { message = "Session invalide." });

    // Récupérer le candidatId depuis userId
    var candidat = await _context.Candidats
        .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

    if (candidat == null)
        return NotFound(new { message = "Candidat introuvable." });

    var candidature = await _context.Candidatures
        .FirstOrDefaultAsync(c => c.CandidatId == candidat.Id && c.OffreId == offreId);

    if (candidature == null)
        return NotFound(new { message = "Candidature introuvable." });

    _context.Candidatures.Remove(candidature);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Candidature annulée avec succès." });
}
// GET /api/candidat/postuler/check/{offreId}
[HttpGet("postuler/check/{offreId:guid}")]
public async Task<IActionResult> CheckAlreadyApplied(Guid offreId)
{
    var userId = GetUserIdFromToken();
    if (userId == null)
        return Unauthorized(new { message = "Session invalide." });

    var hasApplied = await _candidatService.HasAlreadyAppliedAsync(userId.Value, offreId);
    return Ok(new { hasApplied });
}
    }

    // DTO pour le PATCH statut
    public class UpdateStatutDto
    {
        public string Statut { get; set; } = "";
    }
    
}