using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Recrutement_api.Services.Entretien;
using Recrutement_api.DTOs.Entretien;
using System.Security.Claims;

namespace Recrutement_api.Controllers
{
    [ApiController]
    [Route("api/entretiens")]
    public class EntretienController : ControllerBase
    {
        private readonly IEntretienService _service;
        private readonly Recrutement_api.Services.AI.IAiOrchestratorService _aiService;
        
        public EntretienController(IEntretienService service, Recrutement_api.Services.AI.IAiOrchestratorService aiService)
        {
            _service = service;
            _aiService = aiService;
        }

        // ── Tenant/Expert : propose des créneaux et envoie le lien ──
        [HttpPost("{candidatureId}/planifier")]
        [Authorize]
        public async Task<IActionResult> Planifier(Guid candidatureId, [FromBody] PlanifierDto dto)
        {
            var result = await _service.PlanifierAsync(candidatureId, dto);
            return Ok(result);
        }

        // ── Tenant/Expert : liste tous les entretiens ──
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll([FromQuery] Guid? offreId, [FromQuery] string? statut)
        {
            var result = await _service.GetAllAsync(offreId, statut);
            return Ok(result);
        }

        // ── Tenant/Expert : détail d'un entretien ──
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // ── Tenant/Expert : détail d'un entretien par candidature ──
        [HttpGet("candidature/{candidatureId}")]
        [Authorize]
        public async Task<IActionResult> GetByCandidatureId(Guid candidatureId)
        {
            var result = await _service.GetByCandidatureIdAsync(candidatureId);
            if (result == null) return NoContent();
            return Ok(result);
        }

        // ── Tenant/Expert : annuler un entretien ──
        [HttpPost("{id}/annuler")]
        [Authorize]
        public async Task<IActionResult> Annuler(Guid id)
        {
            await _service.AnnulerAsync(id);
            return Ok(new { message = "Entretien annulé" });
        }

        // ── CANDIDAT : ses propres entretiens ──
        [HttpGet("mes-entretiens")]
        [Authorize]
        public async Task<IActionResult> GetMesEntretiens()
        {
            // Essaie d'abord candidatId, sinon userId, sinon sub
            var candidatIdStr = User.FindFirstValue("candidatId")
                             ?? User.FindFirstValue("userId")
                             ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(candidatIdStr))
                return Unauthorized(new { message = "candidatId manquant dans le token" });

            if (!Guid.TryParse(candidatIdStr, out var candidatId))
                return BadRequest(new { message = "candidatId invalide" });

            var result = await _service.GetEntretiensByCandidatAsync(candidatId);
            return Ok(result);
        }

        // ── PUBLIC : candidat voit les créneaux disponibles via son token ──
        [HttpGet("public/{token}/creneaux")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCreneaux(string token)
        {
            var result = await _service.GetCreneauxByTokenAsync(token);
            if (result == null) return NotFound(new { message = "Lien invalide ou expiré" });
            return Ok(result);
        }

        // ── PUBLIC : candidat confirme son créneau ──
        [HttpPost("public/{token}/confirmer")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmerCreneau(string token, [FromBody] ConfirmerCreneauDto dto)
        {
            var result = await _service.ConfirmerCreneauAsync(token, dto.DateChoisie);
            if (result == null) return BadRequest(new { message = "Créneau invalide ou lien expiré" });
            return Ok(result);
        }

        // ── PUBLIC : rejoindre l'entretien ──
        [HttpGet("public/{token}/rejoindre")]
        [AllowAnonymous]
        public async Task<IActionResult> Rejoindre(string token)
        {
            var result = await _service.RejoindreAsync(token);
            if (result == null) return NotFound(new { message = "Lien invalide" });
            if (!result.EstActif) return BadRequest(new
            {
                message = "L'entretien n'est pas encore disponible",
                ouvertureLe = result.DateScheduled
            });
            return Ok(result);
        }

        // ── IA : Générer des questions d'entretien ──
        [HttpGet("ai/generate-questions/{candidatureId}")]
        [Authorize]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> GenerateInterviewQuestions(Guid candidatureId, [FromQuery] int numQuestions = 5, [FromQuery] string langue = "FR")
        {
            var result = await _aiService.GenerateInterviewQuestionsAsync(candidatureId, numQuestions, langue);
            return Ok(result);
        }

        // ── IA : Réponse de l'avatar (disabled — simplified interview flow) ──
        // [HttpPost("{id}/ai/reply")]
        // [AllowAnonymous]
        // public async Task<IActionResult> InterviewReply(Guid id, [FromBody] InterviewReplyDto dto)
        // {
        //     var result = await _aiService.ReplyInterviewAsync(id, dto.QuestionActuelle, dto.ReponseCandidat, dto.Langue);
        //     return Ok(result);
        // }

        // ── CANDIDAT : sauvegarder le transcript ──
        [HttpPatch("{id}/transcript")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveTranscript(Guid id, [FromBody] SaveTranscriptDto dto)
        {
            await _service.SaveTranscriptAsync(id, dto.Transcript);
            return Ok(new { message = "Transcript saved" });
        }

        // ── PUBLIC : voir l'occupation des créneaux ──
        [HttpGet("public/slots-usage")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSlotsUsage([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var result = await _service.GetSlotUsageAsync(start, end);
            return Ok(result);
        }

        // ── IA : Evaluer ──
        [HttpPost("{id}/ai/evaluate")]
        [Authorize]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> EvaluateInterview(Guid id)
        {
            var result = await _aiService.EvaluateInterviewAsync(id);
            return Ok(result);
        }

        // ── IA : TTS ──
        [HttpPost("ai/tts")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateTts([FromBody] TtsDto dto)
        {
            var result = await _aiService.GenerateTtsAsync(dto.Text, dto.Langue);
            return Ok(result);
        }

        // ── SESSION CONTROL ──
        [HttpGet("public/session/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSession(string token)
        {
            try
            {
                var result = await _service.GetSessionAsync(token);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("public/session/{token}/ping")]
        [AllowAnonymous]
        public async Task<IActionResult> PingSession(string token, [FromBody] PingSessionDto dto)
        {
            try
            {
                await _service.PingSessionAsync(token, dto.QuestionIndex, dto.Transcript);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("public/session/{token}/complete")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteSession(string token)
        {
            try
            {
                await _service.CompleteSessionAsync(token);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("public/session/{token}/start")]
        [AllowAnonymous]
        public async Task<IActionResult> StartSession(string token)
        {
            try
            {
                await _service.StartSessionAsync(token);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        [HttpPost("public/session/{token}/stt")]
        [AllowAnonymous]
        public async Task<IActionResult> SpeechToText(string token, IFormFile audio)
        {
            try
            {
                // Verify the session is still valid
                await _service.GetSessionAsync(token);

                if (audio == null || audio.Length == 0)
                    return BadRequest(new { error = "No audio file provided." });

                using var stream = audio.OpenReadStream();
                var result = await _aiService.GenerateSttAsync(stream, audio.FileName);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class PingSessionDto
    {
        public int QuestionIndex { get; set; }
        public string Transcript { get; set; } = string.Empty;
    }
}