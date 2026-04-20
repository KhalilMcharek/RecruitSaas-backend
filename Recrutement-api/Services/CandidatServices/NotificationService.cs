// Services/NotificationService.cs — VERSION DEBUG ULTRA-VERBOSE
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
            System.Diagnostics.Debug.WriteLine("[NotificationService] ✅ Instance créée");
        }

        // ─────────────────────────────────────────────────────────────
        // ★ Notification pour quiz programmé — VERSION DEBUG
        // ─────────────────────────────────────────────────────────────
        public async Task NotifyQuizScheduledAsync(
            Guid candidatId,
            Guid candidatureId,
            string offreTitre,
            DateTime scheduledDate,
            string quizToken,
            int timePerQuestion)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] 🚀 NotifyQuizScheduledAsync called: " +
                $"candidatId={candidatId}, candidatureId={candidatureId}, quizToken={quizToken}");

            try
            {
                // ★ 1. Vérifier que le candidat existe
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 🔍 Checking if Candidat {candidatId} exists...");
                
                var candidatExists = await _context.Candidats
                    .AnyAsync(c => c.Id == candidatId);
                
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 🔍 Candidat exists: {candidatExists}");

                if (!candidatExists)
                {
                    // ★ Log détaillé pour comprendre pourquoi
                    var allCandidats = await _context.Candidats.Select(c => c.Id).ToListAsync();
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] ❌ Candidat {candidatId} NOT FOUND. " +
                        $"Available Candidat IDs: [{string.Join(", ", allCandidats)}]");
                    return;
                }

                // ★ 2. Vérifier que la candidature existe aussi (cohérence)
                var candidatureExists = await _context.Candidatures
                    .AnyAsync(c => c.Id == candidatureId);
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 🔍 Candidature exists: {candidatureExists}");

                // ★ 3. Créer la notification
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 📝 Creating CandidatNotification entity...");

                var notification = new CandidatNotification
                {
                    Id            = Guid.NewGuid(),
                    CandidatId    = candidatId,
                    CandidatureId = candidatureId,
                    Type          = "quiz_invitation",
                    Title         = $"📝 Technical Assessment — {offreTitre}",
                    Body          = $"You have been invited to take a technical quiz for \"{offreTitre}\". " +
                                    $"Scheduled for {scheduledDate:MMM dd, yyyy 'at' HH:mm}. " +
                                    $"Each question has a {timePerQuestion}s time limit.",
                    IsRead        = false,
                    CreeLe        = DateTime.UtcNow,
                    QuizToken     = quizToken
                };

                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 📦 Entity created, Id={notification.Id}, adding to context...");

                _context.CandidatNotifications.Add(notification);

                // ★ 4. Sauvegarder avec gestion d'erreur détaillée
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] 💾 Calling SaveChangesAsync()...");
                
                var rowsAffected = await _context.SaveChangesAsync();
                
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] ✅ SaveChanges completed. Rows affected: {rowsAffected}");

                // ★ 5. Vérification post-insertion
                var check = await _context.CandidatNotifications
                    .FirstOrDefaultAsync(n => n.QuizToken == quizToken);
                
                if (check != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] 🎉 SUCCESS: Notification verified in DB! " +
                        $"Id={check.Id}, CandidatId={check.CandidatId}, Type={check.Type}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] ⚠️ WARNING: Notification NOT found after save. " +
                        $"Token searched: {quizToken}");
                    
                    // Lister les dernières notifications pour debug
                    var recent = await _context.CandidatNotifications
                        .OrderByDescending(n => n.CreeLe)
                        .Take(5)
                        .Select(n => new { n.Id, n.Type, n.CandidatId, n.QuizToken })
                        .ToListAsync();
                    
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] 📋 Last 5 notifications: " +
                        $"{System.Text.Json.JsonSerializer.Serialize(recent)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] ❌❌❌ CRITICAL ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] StackTrace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] InnerException: {ex.InnerException.Message}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // NotifyStatutChangeAsync (inchangé)
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
                case "acceptée": case "accepté": case "accepted":
                    type = "application_accepted";
                    title = "🎉 Application Accepted!";
                    body = $"Congratulations! Your application for \"{offreTitre}\" has been accepted.";
                    break;
                case "refusée": case "refusé": case "rejected":
                    type = "application_rejected";
                    title = "Application Not Selected";
                    body = $"Your application for \"{offreTitre}\" was not selected this time.";
                    break;
                case "entretien": case "interview":
                    type = "interview_scheduled";
                    title = "📅 Interview Scheduled";
                    body = $"An interview has been scheduled for your application to \"{offreTitre}\".";
                    break;
                case "en cours": case "screening": case "in_progress": case "in review":
                    type = "application_in_progress";
                    title = "⏳ Application In Review";
                    body = $"Your application for \"{offreTitre}\" is currently being reviewed.";
                    break;
                default:
                    return;
            }

            await CreateNotificationAsync(candidatId, type, title, body, offreId, candidatureId);
        }

        // ─────────────────────────────────────────────────────────────
        // CreateNotificationAsync (générique)
        // ─────────────────────────────────────────────────────────────
        public async Task<CandidatNotification> CreateNotificationAsync(
            Guid candidatId,
            string type,
            string title,
            string body,
            Guid? offreId = null,
            Guid? candidatureId = null,
            string? quizToken = null)
        {
            var notification = new CandidatNotification
            {
                Id = Guid.NewGuid(),
                CandidatId = candidatId,
                Type = type,
                Title = title,
                Body = body,
                OffreId = offreId,
                CandidatureId = candidatureId,
                QuizToken = quizToken,
                CreeLe = DateTime.UtcNow,
                IsRead = false
            };

            _context.CandidatNotifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        // ─────────────────────────────────────────────────────────────
        // NotifyNewOffreAsync (inchangé)
        // ─────────────────────────────────────────────────────────────
        public async Task NotifyNewOffreAsync(
            Guid offreId,
            string offreTitre,
            string entrepriseNom,
            string? localisation = null)
        {
            var candidatIds = await _context.Candidats.Select(c => c.Id).ToListAsync();
            if (!candidatIds.Any()) return;

            var locationPart = string.IsNullOrWhiteSpace(localisation) ? "" : $" · {localisation}";
            var notifications = candidatIds.Select(candidatId => new CandidatNotification
            {
                Id = Guid.NewGuid(),
                CandidatId = candidatId,
                Type = "new_offer",
                Title = "🆕 New Job Offer",
                Body = $"\"{offreTitre}\" at {entrepriseNom}{locationPart} is now available.",
                OffreId = offreId,
                CandidatureId = null,
                CreeLe = DateTime.UtcNow,
                IsRead = false
            }).ToList();

            _context.CandidatNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid candidatId)
        {
            return await _context.CandidatNotifications
                .CountAsync(n => n.CandidatId == candidatId && !n.IsRead);
        }
    }
}