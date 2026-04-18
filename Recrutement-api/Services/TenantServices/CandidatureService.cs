using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Candidature;
using Recrutement_api.Models;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Services.TenantServices
{
    public class CandidatureService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly CandidateProfileService _profileService;
        private readonly NotificationService _notificationService; // ← AJOUTÉ

        public CandidatureService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            CandidateProfileService profileService,
            NotificationService notificationService) // ← AJOUTÉ
        {
            _context = context;
            _currentUser = currentUser;
            _profileService = profileService;
            _notificationService = notificationService; // ← AJOUTÉ
        }

        public async Task<(bool Success, string Message, CandidateProfileDto? Data)>
            GetCandidateProfileForTenantCandidatureAsync(Guid candidatureId)
        {
            var tenantId = _currentUser.TenantId;

            var candidature = await _context.Candidatures
                .AsNoTracking()
                .Include(c => c.Offre)
                    .ThenInclude(o => o!.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
                return (false, "Candidature introuvable.", null);

            if (candidature.Offre?.Entreprise == null ||
                candidature.Offre.Entreprise.TenantId != tenantId)
                return (false, "Non autorisé.", null);

            var userId = await _context.Candidats
                .AsNoTracking()
                .Where(c => c.Id == candidature.CandidatId)
                .Select(c => c.UtilisateurId)
                .FirstOrDefaultAsync();

            if (userId == Guid.Empty)
                return (false, "Candidat introuvable.", null);

            return await _profileService.GetProfileAsync(userId);
        }

        public async Task<List<CandidatureResponseDto>> GetCandidaturesAsync(
            Guid? entrepriseId, string? nomOffre, string? statut,
            int? scoreMin, int? scoreMax, DateTime? dateDebut, DateTime? dateFin,
            Guid? offreId = null)
        {
            var tenantId = _currentUser.TenantId;
            var query = _context.Candidatures
                .Where(c => c.Offre.Entreprise.TenantId == tenantId)
                .AsQueryable();

            if (offreId.HasValue)
                query = query.Where(c => c.OffreId == offreId.Value);
            if (entrepriseId.HasValue)
                query = query.Where(c => c.Offre.EntrepriseId == entrepriseId.Value);
            if (!string.IsNullOrWhiteSpace(nomOffre))
                query = query.Where(c => EF.Functions.Like(c.Offre.Titre, $"%{nomOffre}%"));
            if (!string.IsNullOrWhiteSpace(statut))
                query = query.Where(c => c.Statut == statut);
            if (dateDebut.HasValue)
                query = query.Where(c => c.CreeLe >= dateDebut.Value);
            if (dateFin.HasValue)
                query = query.Where(c => c.CreeLe <= dateFin.Value.AddDays(1));
            if (scoreMin.HasValue)
                query = query.Where(c => c.AnalyseCV != null && c.AnalyseCV.Score >= scoreMin.Value);
            if (scoreMax.HasValue)
                query = query.Where(c => c.AnalyseCV != null && c.AnalyseCV.Score <= scoreMax.Value);

            return await query
                .OrderByDescending(c => c.CreeLe)
                .Select(c => new CandidatureResponseDto
                {
                    Id            = c.Id,
                    NomCandidat   = c.Candidat.Utilisateur.Nom,
                    EmailCandidat = c.Candidat.Utilisateur.Email,
                    OffreId       = c.OffreId,
                    TitreOffre    = c.Offre.Titre,
                    EntrepriseId  = c.Offre.EntrepriseId,
                    NomEntreprise = c.Offre.Entreprise.Nom,
                    Statut        = c.Statut,
                    CreeLe        = c.CreeLe,
                    CvUrl         = c.CvUrl,
                    ScoreIA       = c.AnalyseCV != null ? c.AnalyseCV.Score : null
                })
                .ToListAsync();
        }

        public async Task<CandidatureStatsDto> GetStatsAsync()
        {
            var tenantId = _currentUser.TenantId;
            var all = await _context.Candidatures
                .Where(c => c.Offre.Entreprise.TenantId == tenantId)
                .Select(c => new { c.Statut, Score = c.AnalyseCV != null ? c.AnalyseCV.Score : null })
                .ToListAsync();

            var scores = all.Where(x => x.Score.HasValue).Select(x => x.Score!.Value).ToList();

            return new CandidatureStatsDto
            {
                Total        = all.Count,
                EnAttente    = all.Count(x => x.Statut == "Nouvelle" || x.Statut == "En cours"),
                Acceptees    = all.Count(x => x.Statut == "Acceptée"),
                Refusees     = all.Count(x => x.Statut == "Refusée"),
                ScoreMoyenIA = scores.Any() ? (float?)scores.Average() : null
            };
        }

        public async Task<object?> GetCandidatureByIdAsync(Guid id)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Offre)
                .ThenInclude(o => o.Entreprise)
                .Include(c => c.Candidat)
                .ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.AnalyseCV)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidature == null) return null;

            object parsedResponses = null;
            if (!string.IsNullOrWhiteSpace(candidature.FormulaireResponses))
            {
                try { parsedResponses = JsonSerializer.Deserialize<Dictionary<string, string>>(candidature.FormulaireResponses); }
                catch { parsedResponses = candidature.FormulaireResponses; }
            }

            return new
            {
                candidature.Id,
                candidature.OffreId,
                candidature.CandidatId,
                NomCandidat       = candidature.Candidat.Utilisateur.Nom,
                EmailCandidat     = candidature.Candidat.Utilisateur.Email,
                TitreOffre        = candidature.Offre.Titre,
                NomEntreprise     = candidature.Offre.Entreprise.Nom,
                candidature.Statut,
                candidature.CreeLe,
                candidature.CvUrl,
                FormulaireResponses = parsedResponses,
                AnalyseCV = candidature.AnalyseCV != null ? new
                {
                    candidature.AnalyseCV.Id,
                    candidature.AnalyseCV.Score,
                    candidature.AnalyseCV.Classification,
                    candidature.AnalyseCV.Resume,
                    candidature.AnalyseCV.Competences,
                    candidature.AnalyseCV.Experience
                } : null
            };
        }

        public async Task<List<CandidatureResponseDto>> GetMesCandidaturesDirectAsync(Guid candidatId)
        {
            Console.WriteLine($"[Direct] CandidatId reçu = {candidatId}");

            var result = await _context.Candidatures
                .Where(c => c.CandidatId == candidatId)
                .OrderByDescending(c => c.CreeLe)
                .Select(c => new CandidatureResponseDto
                {
                    Id            = c.Id,
                    NomCandidat   = c.Candidat.Utilisateur.Nom,
                    EmailCandidat = c.Candidat.Utilisateur.Email,
                    OffreId       = c.OffreId,
                    TitreOffre    = c.Offre.Titre,
                    EntrepriseId  = c.Offre.EntrepriseId,
                    NomEntreprise = c.Offre.Entreprise.Nom,
                    Statut        = c.Statut,
                    CreeLe        = c.CreeLe,
                    ScoreIA       = c.AnalyseCV != null ? c.AnalyseCV.Score : null
                })
                .ToListAsync();

            Console.WriteLine($"[Direct] Résultats = {result.Count}");
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // ★ CHANGER STATUT — notifie le candidat si le statut change
        //   Même pattern que OffreService.ChangerStatutPublicationAsync()
        // ─────────────────────────────────────────────────────────────
        public async Task<object> ChangerStatutAsync(Guid candidatureId, string nouveauStatut)
        {
            // 1 — Charger la candidature avec toutes les relations nécessaires
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat)       // ← pour candidatId
                .Include(c => c.Offre)          // ← pour offreTitre + offreId
                .FirstOrDefaultAsync(c => c.Id == candidatureId)
                ?? throw new KeyNotFoundException("Candidature introuvable");

            var ancienStatut = candidature.Statut;

            // 2 — Mettre à jour le statut
            candidature.Statut = nouveauStatut;
            await _context.SaveChangesAsync();

            // 3 — ★ Notifier le candidat si le statut a vraiment changé
            //     Même logique que : if (publier && !ancienStatut) NotifyNewOffreAsync(...)
            if (ancienStatut != nouveauStatut && candidature.Candidat != null)
            {
                await _notificationService.NotifyStatutChangeAsync(
                    candidatId:    candidature.Candidat.Id,
                    candidatureId: candidature.Id,
                    offreTitre:    candidature.Offre?.Titre ?? "Unknown Position",
                    nouveauStatut: nouveauStatut,
                    offreId:       candidature.Offre?.Id
                );
            }

            // 4 — Notifier les experts assignés (logique existante conservée)
            if (ancienStatut != nouveauStatut)
            {
                var expertsAssignes = await _context.AssignationsExperts
                    .Where(a => a.OffreId == candidature.OffreId)
                    .Select(a => a.ExpertId)
                    .ToListAsync();

                var nom = candidature.Candidat?.Utilisateur != null
                    ? $"{candidature.Candidat.Utilisateur.Prenom} {candidature.Candidat.Utilisateur.Nom}".Trim()
                    : "A candidate";

                foreach (var expId in expertsAssignes)
                {
                    _context.Notifications.Add(new Notification
                    {
                        ExpertId   = expId,
                        Type       = "status_updates",
                        Title      = "Candidate status updated",
                        Body       = $"{nom}'s application for \"{candidature.Offre?.Titre}\" is now \"{nouveauStatut}\"",
                        OffreId    = candidature.OffreId,
                        CandidatId = candidature.Id,
                        CreeLe     = DateTime.UtcNow,
                        Read       = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            return new { id = candidature.Id, statut = nouveauStatut };
        }
    }
}