using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;

namespace Recrutement_api.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─────────────────────────────────────────────────────────────
        // Création générique d'une notification
        // ─────────────────────────────────────────────────────────────
        public async Task<CandidatNotification> CreateNotificationAsync(
            Guid candidatId,
            string type,
            string title,
            string body,
            Guid? offreId = null,
            Guid? candidatureId = null)
        {
            var notification = new CandidatNotification
            {
                Id             = Guid.NewGuid(),
                CandidatId     = candidatId,
                Type           = type,
                Title          = title,
                Body           = body,
                OffreId        = offreId,
                CandidatureId  = candidatureId,
                CreeLe         = DateTime.UtcNow,
                IsRead         = false
            };

            _context.CandidatNotifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        // ─────────────────────────────────────────────────────────────
        // ★ Notification automatique lors d'un changement de statut
        //   Appelé depuis CandidatController.UpdateStatut()
        // ─────────────────────────────────────────────────────────────
        public async Task NotifyStatutChangeAsync(
            Guid candidatId,
            Guid candidatureId,
            string offreTitre,
            string nouveauStatut,
            Guid? offreId = null)
        {
            string type, title, body;

            switch (nouveauStatut.ToLower().Trim())
            {
                case "acceptée":
                case "accepté":
                case "accepted":
                    type  = "application_accepted";
                    title = "🎉 Application Accepted!";
                    body  = $"Congratulations! Your application for \"{offreTitre}\" has been accepted.";
                    break;

                case "refusée":
                case "refusé":
                case "rejected":
                    type  = "application_rejected";
                    title = "Application Not Selected";
                    body  = $"Your application for \"{offreTitre}\" was not selected this time.";
                    break;

                case "entretien":
                case "interview":
                    type  = "interview_scheduled";
                    title = "📅 Interview Scheduled";
                    body  = $"An interview has been scheduled for your application to \"{offreTitre}\".";
                    break;

                case "en cours":
                case "screening":
                case "in_progress":
                case "in review":
                    type  = "application_in_progress";
                    title = "⏳ Application In Review";
                    body  = $"Your application for \"{offreTitre}\" is currently being reviewed.";
                    break;

                default:
                    // Statut "Nouvelle" ou inconnu → pas de notification
                    return;
            }

            await CreateNotificationAsync(candidatId, type, title, body, offreId, candidatureId);
        }

        // ─────────────────────────────────────────────────────────────
        // ★ Notification pour une nouvelle offre publiée
        // ─────────────────────────────────────────────────────────────
        public async Task NotifyNewOffreAsync(
            Guid offreId,
            string offreTitre,
            string entrepriseNom,
            string? localisation = null)
        {
            var candidatIds = await _context.Candidats
                .Select(c => c.Id)
                .ToListAsync();

            if (!candidatIds.Any()) return;

            var locationPart = string.IsNullOrWhiteSpace(localisation) ? "" : $" · {localisation}";
            var notifications = candidatIds.Select(candidatId => new CandidatNotification
            {
                Id            = Guid.NewGuid(),
                CandidatId    = candidatId,
                Type          = "new_offer",
                Title         = "🆕 New Job Offer",
                Body          = $"\"{offreTitre}\" at {entrepriseNom}{locationPart} is now available.",
                OffreId       = offreId,
                CandidatureId = null,
                CreeLe        = DateTime.UtcNow,
                IsRead        = false
            }).ToList();

            _context.CandidatNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // Utilitaire : compter les non-lues d'un candidat
        // ─────────────────────────────────────────────────────────────
        public async Task<int> GetUnreadCountAsync(Guid candidatId)
        {
            return await _context.CandidatNotifications
                .CountAsync(n => n.CandidatId == candidatId && !n.IsRead);
        }
    }
}