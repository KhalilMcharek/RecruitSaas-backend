using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.Services.CandidatServices;
using Recrutement_api.Services.CvExtraction;
using System.Security.Claims;

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

        public CandidatController(
            ICandidatService candidatService,
            ICvExtractionService cvExtractionService,
            ILogger<CandidatController> logger)
        {
            _candidatService = candidatService;
            _cvExtractionService = cvExtractionService;
            _logger          = logger;
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

        // ─────────────────────────────────────────────────────────────
        // Private helper — maps ServiceErrorType → HTTP response
        // ─────────────────────────────────────────────────────────────
        
    }
}