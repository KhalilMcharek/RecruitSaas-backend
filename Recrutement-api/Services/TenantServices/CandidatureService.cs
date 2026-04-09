using System.Text.Json;
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

            // Deserialize the FormulaireResponses JSON
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
                NomCandidat = candidature.Candidat.Utilisateur.Nom,
                EmailCandidat = candidature.Candidat.Utilisateur.Email,
                TitreOffre = candidature.Offre.Titre,
                NomEntreprise = candidature.Offre.Entreprise.Nom,
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
                    candidature.AnalyseCV.Experience,
                    candidature.AnalyseCV.TexteExtrait
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
}}
}
