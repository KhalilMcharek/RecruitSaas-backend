using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public EntretienController(IEntretienService service) => _service = service;

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
    }
}