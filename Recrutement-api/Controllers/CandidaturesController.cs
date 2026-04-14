using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.Services.AI;
using System.Net.Http;


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

        private IActionResult FromAiUpstream(AiUpstreamException ex)
        {
            var detail = ex.ResponseBody ?? string.Empty;
            if (detail.Length > 4000)
                detail = detail[..4000] + "…";
            var code = ex.HttpStatus is >= 400 and <= 599 ? ex.HttpStatus : 502;
            return StatusCode(code, new { message = ex.Message, detail });
        }

        private async Task<IActionResult> RunAi(Func<Task<object>> work)
        {
            try
            {
                return Ok(await work());
            }
            catch (AiUpstreamException ex)
            {
                return FromAiUpstream(ex);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, new
                {
                    message = "Impossible de joindre le service IA (réseau ou URL).",
                    detail = ex.Message
                });
            }
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var candidature = await _candidatureService.GetCandidatureByIdAsync(id);
            if (candidature == null)
                return NotFound(new { message = "Candidature non trouvée" });

            return Ok(candidature);
        }

        // ======================================
        // AI Endpoints
        // ======================================

        [HttpPost("{id}/ai/score")]
        public Task<IActionResult> CalculateScore(Guid id) =>
            RunAi(() => _aiService.CalculateScoreAsync(id));

        [HttpPost("{id}/ai/classify")]
        public Task<IActionResult> Classify(Guid id) =>
            RunAi(() => _aiService.ClassifyCandidateAsync(id));

        /// <summary>Profil CV (domaine, niveau, type de poste) via IA — sans lien avec /ai/classify.</summary>
        [HttpPost("{id}/ai/profile")]
        public Task<IActionResult> ProfileFromCv(Guid id) =>
            RunAi(() => _aiService.ProfileFromCvAsync(id));

        [HttpPost("{id}/ai/summarize")]
        public Task<IActionResult> Summarize(Guid id) =>
            RunAi(() => _aiService.SummarizeCvAsync(id));

        [HttpPost("{id}/ai/extract-skills")]
        public Task<IActionResult> ExtractSkills(Guid id) =>
            RunAi(() => _aiService.ExtractCompetencesAsync(id));

        [HttpPost("{id}/ai/extract-experience")]
        public Task<IActionResult> ExtractExperience(Guid id) =>
            RunAi(() => _aiService.ExtractExperienceAsync(id));

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
    }
}
