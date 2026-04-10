using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.DTOs.Candidature;
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

        public CandidaturesController(
            CandidatureService candidatureService,
            IAiOrchestratorService aiService)
        {
            _candidatureService = candidatureService;
            _aiService = aiService;
        }

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

        // ================= AI =================

        [HttpPost("{id}/ai/score")]
        public async Task<IActionResult> CalculateScore(Guid id)
        {
            var res = await _aiService.CalculateScoreAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/classify")]
        public async Task<IActionResult> Classify(Guid id)
        {
            var res = await _aiService.ClassifyCandidateAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/summarize")]
        public async Task<IActionResult> Summarize(Guid id)
        {
            var res = await _aiService.SummarizeCvAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/extract-skills")]
        public async Task<IActionResult> ExtractSkills(Guid id)
        {
            var res = await _aiService.ExtractCompetencesAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/extract-experience")]
        public async Task<IActionResult> ExtractExperience(Guid id)
        {
            var res = await _aiService.ExtractExperienceAsync(id);
            return Ok(res);
        }

        // ================= MES CANDIDATURES =================

        [HttpGet("mes-candidatures")]
        public async Task<IActionResult> GetMesCandidatures()
        {
            var candidatIdStr = User.FindFirstValue("candidatId");

            if (string.IsNullOrEmpty(candidatIdStr))
                return Unauthorized(new { message = "candidatId manquant dans le token." });

            if (!Guid.TryParse(candidatIdStr, out var candidatId))
                return BadRequest(new { message = "candidatId invalide dans le token." });

            var result = await _candidatureService.GetMesCandidaturesDirectAsync(candidatId);
            return Ok(result);
        }

        [HttpPatch("{candidatureId}/statut")]
        public async Task<IActionResult> ChangerStatut(
            Guid candidatureId,
            [FromBody] ChangerStatutDto dto)
        {
            var statutsValides = new[]
            {
                "Nouvelle", "En cours", "Présélectionné",
                "Entretien", "Acceptée", "Refusée"
            };

            if (!statutsValides.Contains(dto.Statut))
                return BadRequest(new
                {
                    message = $"Statut invalide. Valeurs acceptées : {string.Join(", ", statutsValides)}"
                });

            try
            {
                var result = await _candidatureService
                    .ChangerStatutAsync(candidatureId, dto.Statut);

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}