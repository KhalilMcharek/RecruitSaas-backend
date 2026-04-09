using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Candidature;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;


namespace Recrutement_api.Services.TenantServices
{
    public class CandidatureService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public CandidatureService(ApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<CandidatureResponseDto>> GetCandidaturesAsync(
            Guid? entrepriseId, string? nomOffre, string? statut,
            int? scoreMin, int? scoreMax, DateTime? dateDebut, DateTime? dateFin)
        {
            var tenantId = _currentUser.TenantId;
            var query = _context.Candidatures
                .Where(c => c.Offre.Entreprise.TenantId == tenantId)
                .AsQueryable();

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

        public async Task<object> ChangerStatutAsync(Guid candidatureId, string nouveauStatut)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat).ThenInclude(c => c.Utilisateur)
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId)
                ?? throw new KeyNotFoundException("Candidature introuvable");

            var ancienStatut = candidature.Statut;
            candidature.Statut = nouveauStatut;
            await _context.SaveChangesAsync();

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