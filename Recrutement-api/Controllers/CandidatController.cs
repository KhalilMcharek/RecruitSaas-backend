// Controllers/CandidatController.cs
// ─── VERSION CORRIGÉE : inclut QuizToken dans les notifications ───

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.CandidatServices;
using Recrutement_api.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Models;
using Recrutement_api.Data;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CandidatController : ControllerBase
    {
        private readonly ICandidatService        _candidatService;
        private readonly CandidateProfileService _profileService;
        private readonly ApplicationDbContext    _context;
        private readonly NotificationService     _notifService;

        public CandidatController(
            ICandidatService        candidatService,
            CandidateProfileService profileService,
            ApplicationDbContext    context,
            NotificationService     notifService)
        {
            _candidatService = candidatService;
            _profileService  = profileService;
            _context         = context;
            _notifService    = notifService;
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

        // ─────────────────────────────────────────────
        // PROFILE GET
        // ─────────────────────────────────────────────
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

        // ─────────────────────────────────────────────
        // PROFILE UPDATE
        // ─────────────────────────────────────────────
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

        // ─────────────────────────────────────────────
        // GET candidature by id
        // ─────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var candidature = await _candidatService.GetCandidatureByIdAsync(id);
            if (candidature == null)
                return NotFound(new { message = "Candidature introuvable." });

            return Ok(candidature);
        }

        // ─────────────────────────────────────────────────────────────
        // ★ PATCH /api/candidat/{id}/statut
        //   Met à jour le statut ET crée la notification automatiquement
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/statut")]
        public async Task<IActionResult> UpdateStatut(Guid id, [FromBody] UpdateStatutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Statut))
                return BadRequest(new { message = "Statut invalide." });

            var allowed = new[] { "Nouvelle", "En cours", "Entretien", "Acceptée", "Refusée" };
            if (!allowed.Contains(dto.Statut))
                return BadRequest(new { message = $"Statut non reconnu. Valeurs autorisées : {string.Join(", ", allowed)}" });

            var candidature = await _context.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null)
                return NotFound(new { message = "Candidature introuvable." });

            candidature.Statut = dto.Statut;
            await _context.SaveChangesAsync();

            var offreTitre = candidature.Offre?.Titre ?? "Unknown Position";
            var offreId    = candidature.Offre?.Id;

            await _notifService.NotifyStatutChangeAsync(
                candidatId:     candidature.Candidat.Id,
                candidatureId:  candidature.Id,
                offreTitre:     offreTitre,
                nouveauStatut:  dto.Statut,
                offreId:        offreId
            );

            return Ok(new { message = "Statut mis à jour.", statut = dto.Statut });
        }

        // ═══════════════════════════════════════════
        // DELETE /api/candidat/postuler/{offreId}
        // ═══════════════════════════════════════════
        [HttpDelete("postuler/{offreId:guid}")]
        public async Task<IActionResult> CancelCandidature(Guid offreId)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var success = await _candidatService.CancelCandidatureAsync(userId.Value, offreId);

            if (!success)
                return NotFound(new { message = "Candidature introuvable ou déjà annulée." });

            return Ok(new { message = "Candidature annulée avec succès." });
        }

        // ═══════════════════════════════════════════
        // GET /api/candidat/postuler/check/{offreId}
        // ═══════════════════════════════════════════
        [HttpGet("postuler/check/{offreId:guid}")]
        public async Task<IActionResult> CheckAlreadyApplied(Guid offreId)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var hasApplied = await _candidatService.HasAlreadyAppliedAsync(userId.Value, offreId);
            return Ok(new { hasApplied });
        }

        // ═════════════════════════════════════════════════════════
        // ★ GET /api/candidat/notifications — CORRIGÉ : inclut QuizToken
        // ═════════════════════════════════════════════════════════
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            // ★ PROJECTION COMPLÈTE avec QuizToken + QuizUrl calculé
            var notifications = await _context.CandidatNotifications
                .Where(n => n.CandidatId == candidat.Id)
                .OrderByDescending(n => n.CreeLe)
                .Take(50)
                .Select(n => new
                {
                    n.Id,
                    n.Type,
                    n.Title,
                    n.Body,
                    n.IsRead,
                    n.CreeLe,
                    n.LuLe,
                    n.OffreId,
                    n.CandidatureId,
                    
                    // ★ CHAMPS SPÉCIFIQUES QUIZ — CRITIQUES POUR LE FRONTEND
                    QuizToken = n.QuizToken,
                    QuizUrl   = n.QuizToken != null ? $"/quiz/{n.QuizToken}" : null
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // ═════════════════════════════════════════════════════════
        // GET /api/candidat/notifications/unread-count
        // ═════════════════════════════════════════════════════════
        [HttpGet("notifications/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            var count = await _context.CandidatNotifications
                .CountAsync(n => n.CandidatId == candidat.Id && !n.IsRead);

            return Ok(new { count });
        }

        // ═════════════════════════════════════════════════════════
        // PATCH /api/candidat/notifications/{id}/read
        // ═════════════════════════════════════════════════════════
        [HttpPatch("notifications/{id:guid}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            var notification = await _context.CandidatNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.CandidatId == candidat.Id);

            if (notification == null)
                return NotFound(new { message = "Notification introuvable." });

            notification.IsRead = true;
            notification.LuLe   = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Notification marquée comme lue." });
        }

        // ═════════════════════════════════════════════════════════
        // PATCH /api/candidat/notifications/mark-all-read
        // ═════════════════════════════════════════════════════════
        [HttpPatch("notifications/mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            var notifications = await _context.CandidatNotifications
                .Where(n => n.CandidatId == candidat.Id && !n.IsRead)
                .ToListAsync();

            foreach (var notif in notifications)
            {
                notif.IsRead = true;
                notif.LuLe   = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Toutes les notifications ont été marquées comme lues.", count = notifications.Count });
        }

        // ═════════════════════════════════════════════════════════
        // DELETE /api/candidat/notifications/{id}
        // ═════════════════════════════════════════════════════════
        [HttpDelete("notifications/{id:guid}")]
        public async Task<IActionResult> DeleteNotification(Guid id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            var notification = await _context.CandidatNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.CandidatId == candidat.Id);

            if (notification == null)
                return NotFound(new { message = "Notification introuvable." });

            _context.CandidatNotifications.Remove(notification);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Notification supprimée." });
        }

        // ═════════════════════════════════════════════════════════
        // DELETE /api/candidat/notifications/clear-old
        // ═════════════════════════════════════════════════════════
        [HttpDelete("notifications/clear-old")]
        public async Task<IActionResult> ClearOldNotifications()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId.Value);

            if (candidat == null)
                return NotFound(new { message = "Candidat introuvable." });

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var oldNotifications = await _context.CandidatNotifications
                .Where(n => n.CandidatId == candidat.Id && n.CreeLe < thirtyDaysAgo)
                .ToListAsync();

            _context.CandidatNotifications.RemoveRange(oldNotifications);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Anciennes notifications supprimées.", count = oldNotifications.Count });
        }
    }

    // ─────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────
    public class UpdateStatutDto
    {
        public string Statut { get; set; } = "";
    }
}