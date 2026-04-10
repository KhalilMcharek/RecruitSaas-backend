using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.Services.AI;


namespace Recrutement_api.Controllers
{
    [Route("api/candidatures")]
    [ApiController]
    [Authorize]
    public class CandidaturesController : ControllerBase
    {
        private readonly CandidatureService _candidatureService;
        private readonly IAiOrchestratorService _aiService;

        public CandidaturesController(CandidatureService candidatureService, IAiOrchestratorService aiService)
        {
            _candidatureService = candidatureService;
            _aiService = aiService;
        }

        /// <summary>
        /// GET /api/candidatures
        /// Supports query params: entrepriseId, offreId, statut, scoreMin, scoreMax, dateDebut, dateFin
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCandidatures(
            [FromQuery] Guid? entrepriseId,
            [FromQuery] string? nomOffre,
            [FromQuery] string? statut,
            [FromQuery] int? scoreMin,
            [FromQuery] int? scoreMax,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin)
        {
            var result = await _candidatureService.GetCandidaturesAsync(
                entrepriseId, nomOffre, statut, scoreMin, scoreMax, dateDebut, dateFin);

            return Ok(result);
        }

        /// <summary>
        /// GET /api/candidatures/stats
        /// Returns aggregate stats (total, pending, accepted, rejected, avg AI score)
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _candidatureService.GetStatsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// GET /api/candidatures/{candidatureId}/candidate-profile
        /// Profil (CandidateProfiles) du candidat pour une candidature du tenant.
        /// </summary>
        [HttpGet("{candidatureId:guid}/candidate-profile")]
        public async Task<IActionResult> GetCandidateProfile(Guid candidatureId)
        {
            var (success, message, data) =
                await _candidatureService.GetCandidateProfileForTenantCandidatureAsync(candidatureId);

            if (!success)
                return NotFound(new { success = false, message });

            return Ok(new { success = true, data });
        }

[HttpGet("mes-candidatures")]
        public async Task<IActionResult> GetMesCandidatures()
        {
            // Debug : afficher tous les claims pour diagnostic
            foreach (var claim in User.Claims)
                Console.WriteLine($"[JWT CLAIM] {claim.Type} = {claim.Value}");
 
            var candidatIdStr = User.FindFirstValue("candidatId");
            Console.WriteLine($"[mes-candidatures] candidatId = {candidatIdStr}");
 
            if (string.IsNullOrEmpty(candidatIdStr))
                return Unauthorized(new { message = "candidatId manquant dans le token. Vérifiez la génération du JWT." });
 
            if (!Guid.TryParse(candidatIdStr, out var candidatId))
                return BadRequest(new { message = "candidatId invalide dans le token." });
 
            var result = await _candidatureService.GetMesCandidaturesDirectAsync(candidatId);
            return Ok(result);
        }
    }}
