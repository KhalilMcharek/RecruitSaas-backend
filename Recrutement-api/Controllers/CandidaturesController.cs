using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;


namespace Recrutement_api.Controllers
{
    [Route("api/candidatures")]
    [ApiController]
    [Authorize]
    public class CandidaturesController : ControllerBase
    {
        private readonly CandidatureService _candidatureService;

        public CandidaturesController(CandidatureService candidatureService)
        {
            _candidatureService = candidatureService;
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
