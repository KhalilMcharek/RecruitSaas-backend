using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Candidature;
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

        /// <summary>
        /// Returns all candidatures for the current tenant with optional filters:
        /// entrepriseId, offreId, statut, scoreMin, scoreMax, dateDebut, dateFin
        /// </summary>
        public async Task<List<CandidatureResponseDto>> GetCandidaturesAsync(
            Guid? entrepriseId,
           string? nomOffre,
            string? statut,
            int? scoreMin,
            int? scoreMax,
            DateTime? dateDebut,
            DateTime? dateFin)
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
                    Id = c.Id,
                    NomCandidat = c.Candidat.Utilisateur.Nom,
                    EmailCandidat = c.Candidat.Utilisateur.Email,
                    OffreId = c.OffreId,
                    TitreOffre = c.Offre.Titre,
                    EntrepriseId = c.Offre.EntrepriseId,
                    NomEntreprise = c.Offre.Entreprise.Nom,
                    Statut = c.Statut,
                    CreeLe = c.CreeLe,
                    ScoreIA = c.AnalyseCV != null ? c.AnalyseCV.Score : null
                })
                .ToListAsync();
        }

        /// <summary>
        /// Returns stats for the current tenant (total, by status, average AI score).
        /// </summary>
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
                Total = all.Count,
                EnAttente = all.Count(x => x.Statut == "Nouvelle" || x.Statut == "En cours"),
                Acceptees = all.Count(x => x.Statut == "Acceptée"),
                Refusees = all.Count(x => x.Statut == "Refusée"),
                ScoreMoyenIA = scores.Any() ? (float?)scores.Average() : null
            };
        }
    }
}
