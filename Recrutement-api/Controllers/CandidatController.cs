using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.CandidatServices;
using System.Security.Claims;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CandidatController : ControllerBase
    {
        private readonly ICandidatService _candidatService;
        private readonly CandidateProfileService _profileService;

        public CandidatController(
            ICandidatService candidatService,
            CandidateProfileService profileService)
        {
            _candidatService = candidatService;
            _profileService = profileService;
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
        // ─────────────────────────────────────────────
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler([FromForm] PostulerDto dto)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var result = await _candidatService.PostulerAsync(userId.Value, dto);

            return Ok(result);
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
        // GET /api/candidat/profile
        // ═════════════════════════════════════════════════════════
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdFromToken();
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
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Non autorisé" });
 
            var (success, message, data) = await _profileService.UpdateProfileAsync(userId.Value, dto, avatar);
 
            if (!success)
                return BadRequest(new { success = false, message });
 
            return Ok(new { success = true, message, data });
        }
        // ─────────────────────────────────────────────────────────────
        // GET candidature by id
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var candidature = await _candidatService.GetCandidatureByIdAsync(id);
            if (candidature == null)
                return NotFound(new { message = "Candidature introuvable." });

            return Ok(candidature);
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH candidature statut
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/statut")]
        public async Task<IActionResult> UpdateStatut(Guid id, [FromBody] UpdateStatutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Statut))
                return BadRequest(new { message = "Statut invalide." });

            var allowed = new[] { "Nouvelle", "En cours", "Acceptée", "Refusée" };
            if (!allowed.Contains(dto.Statut))
                return BadRequest(new { message = $"Statut non reconnu. Valeurs autorisées : {string.Join(", ", allowed)}" });

            var success = await _candidatService.UpdateCandidatureStatutAsync(id, dto.Statut);
            if (!success)
                return NotFound(new { message = "Candidature introuvable." });

            return Ok(new { message = "Statut mis à jour.", statut = dto.Statut });
        }
    }

    public class UpdateStatutDto
    {
        public string Statut { get; set; } = "";
    }
}
