using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.DTOs.Candidature;
using Recrutement_api.Services.AI;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;

namespace Recrutement_api.Controllers
{
    [Route("api/candidatures")]
    [ApiController]
    [Authorize]
    public class CandidaturesController : ControllerBase
    {
        private readonly CandidatureService _candidatureService;
        private readonly IAiOrchestratorService _aiService;
        private readonly ApplicationDbContext _context;

        public CandidaturesController(
            CandidatureService candidatureService,
            IAiOrchestratorService aiService,
            ApplicationDbContext context)
        {
            _candidatureService = candidatureService;
            _aiService          = aiService;
            _context            = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCandidatures(
            [FromQuery] Guid? entrepriseId,
            [FromQuery] string? nomOffre,
            [FromQuery] string? statut,
            [FromQuery] int? scoreMin,
            [FromQuery] int? scoreMax,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] Guid? offreId)
        {
            var result = await _candidatureService.GetCandidaturesAsync(
                entrepriseId, nomOffre, statut, scoreMin, scoreMax, dateDebut, dateFin, offreId);
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

        // ── Avis experts ──────────────────────────────────────────────────────
        [HttpGet("{id}/avis-experts")]
        public async Task<IActionResult> GetAvisExperts(Guid id)
        {
            var candidature = await _context.Candidatures
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null)
                return NotFound(new { message = "Candidature non trouvée" });

            var avis = await _context.AvisExperts
                .Include(a => a.Expert)
                .Where(a => a.CandidatureId == id)
                .OrderByDescending(a => a.CreeLe)
                .Select(a => new
                {
                    id          = a.Id,
                    expertId    = a.ExpertId,
                    expertNom   = a.Expert != null
                                    ? (a.Expert.FirstName + " " + a.Expert.LastName).Trim()
                                    : "Expert",
                    expertPoste = a.Expert != null ? a.Expert.Poste : null,
                    score       = a.Score,
                    scorePct    = (int)Math.Round(a.Score * 20),
                    commentaire = a.Commentaire,
                    creeLe      = a.CreeLe
                })
                .ToListAsync();

            double? scoreMoyen = avis.Any()
                ? Math.Round(avis.Average(a => a.score), 2)
                : null;

            return Ok(new { avis, scoreMoyen, nombreAvis = avis.Count });
        }

        // ── Experts assignés ──────────────────────────────────────────────────
        [HttpGet("{id}/experts-assignes")]
        public async Task<IActionResult> GetExpertsAssignes(Guid id)
        {
            var candidature = await _context.Candidatures
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null)
                return NotFound(new { message = "Candidature non trouvée" });

            var experts = await _context.AssignationsExperts
                .Include(a => a.Expert)
                .Where(a => a.OffreId == candidature.OffreId)
                .Select(a => new
                {
                    expertId  = a.ExpertId,
                    nom       = a.Expert != null
                                 ? (a.Expert.FirstName + " " + a.Expert.LastName).Trim()
                                 : "Expert",
                    poste     = a.Expert != null ? a.Expert.Poste : null,
                    isActive  = a.Expert != null ? a.Expert.IsActive : false,
                    assigneLe = a.AssigneLe
                })
                .ToListAsync();

            return Ok(experts);
        }

        // ── Profil candidat (collègue) ────────────────────────────────────────
        [HttpGet("{candidatureId:guid}/candidate-profile")]
        public async Task<IActionResult> GetCandidateProfile(Guid candidatureId)
        {
            var (success, message, data) =
                await _candidatureService.GetCandidateProfileForTenantCandidatureAsync(candidatureId);

            if (!success)
                return Ok(new { success = false, message });

            return Ok(new { success = true, data });
        }

        // ================= AI =================

        [HttpPost("{id}/ai/score")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> CalculateScore(Guid id)
        {
            var res = await _aiService.CalculateScoreAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/classify")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> Classify(Guid id)
        {
            var res = await _aiService.ClassifyCandidateAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/summarize")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> Summarize(Guid id)
        {
            var res = await _aiService.SummarizeCvAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/extract-skills")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> ExtractSkills(Guid id)
        {
            var res = await _aiService.ExtractCompetencesAsync(id);
            return Ok(res);
        }

        [HttpPost("{id}/ai/extract-experience")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> ExtractExperience(Guid id)
        {
            var res = await _aiService.ExtractExperienceAsync(id);
            return Ok(res);
        }
        [HttpPost("{id}/ai/extract-certifications")]
        [EnableRateLimiting("AiServicesPolicy")]
        public async Task<IActionResult> ExtractCertifications(Guid id)
        {
            var res = await _aiService.ExtractCertificationsAsync(id);
            return Ok(res);
        }
        // ================= INTERVIEW =================

        [HttpGet("{id}/interview/context")]
        public async Task<IActionResult> GetInterviewContext(Guid id)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat).ThenInclude(c => c.Utilisateur)
                .Include(c => c.Offre).ThenInclude(o => o.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null)
                return NotFound(new { message = "Candidature non trouvée" });

            var candidatNom = candidature.Candidat?.Utilisateur != null
                ? $"{candidature.Candidat.Utilisateur.Prenom} {candidature.Candidat.Utilisateur.Nom}".Trim()
                : "Candidat";

            return Ok(new
            {
                candidatureId  = candidature.Id,
                sessionId      = candidature.Id,
                candidateName  = candidatNom,
                candidateEmail = candidature.Candidat?.Utilisateur?.Email,
                jobTitle       = candidature.Offre?.Titre ?? "Poste",
                companyName    = candidature.Offre?.Entreprise?.Nom ?? "RecruitSaaS",
                status         = candidature.Statut
            });
        }

        [HttpPost("{id}/interview/start")]
        public async Task<IActionResult> StartInterview(Guid id)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat).ThenInclude(c => c.Utilisateur)
                .Include(c => c.Offre).ThenInclude(o => o.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null)
                return NotFound(new { message = "Candidature non trouvée" });

            if (!string.Equals(candidature.Statut, "Entretien", StringComparison.OrdinalIgnoreCase))
            {
                candidature.Statut = "Entretien";
                await _context.SaveChangesAsync();
            }

            var candidatNom = candidature.Candidat?.Utilisateur != null
                ? $"{candidature.Candidat.Utilisateur.Prenom} {candidature.Candidat.Utilisateur.Nom}".Trim()
                : "Candidat";

            return Ok(new
            {
                interviewSessionId = candidature.Id,
                candidatureId      = candidature.Id,
                status             = candidature.Statut,
                candidateName      = candidatNom,
                jobTitle           = candidature.Offre?.Titre ?? "Poste",
                companyName        = candidature.Offre?.Entreprise?.Nom ?? "RecruitSaaS"
            });
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
                var result = await _candidatureService.ChangerStatutAsync(candidatureId, dto.Statut);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex)            { return StatusCode(500, new { message = ex.Message }); }
        }
    }
}