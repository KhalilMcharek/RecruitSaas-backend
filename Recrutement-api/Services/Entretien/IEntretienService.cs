using Recrutement_api.DTOs.Entretien;

namespace Recrutement_api.Services.Entretien
{
    public interface IEntretienService
    {
        Task<object> PlanifierAsync(Guid candidatureId, PlanifierDto dto);
        Task<List<object>> GetAllAsync(Guid? offreId, string? statut);
        Task<object?> GetByCandidatureIdAsync(Guid candidatureId);
        Task<object?> GetByIdAsync(Guid id);
        Task AnnulerAsync(Guid id);
        Task<object?> GetCreneauxByTokenAsync(string token);
        Task<object?> ConfirmerCreneauAsync(string token, DateTime dateChoisie);
        Task<EntretienRejoindreResult?> RejoindreAsync(string token);
        Task<List<object>> GetEntretiensByCandidatAsync(Guid candidatId);
        Task SaveTranscriptAsync(Guid id, string transcript);
        Task<Dictionary<DateTime, int>> GetSlotUsageAsync(DateTime start, DateTime end);
        
        // Session endpoints
        Task<object> GetSessionAsync(string token);
        Task PingSessionAsync(string token, int questionIndex, string transcript);
        Task CompleteSessionAsync(string token);
        Task StartSessionAsync(string token);
    }

    public class EntretienRejoindreResult
    {
        public bool EstActif { get; set; }
        public DateTime? DateScheduled { get; set; }
        public string? LienEntretien { get; set; }
        public string? NomCandidat { get; set; }
        public string? TitreOffre { get; set; }
        public Guid EntretienId { get; set; }
        public string Langue { get; set; } = "FR";

        /// <summary>URL absolue de la photo de profil (CandidateProfiles) pour comparaison visage côté client.</summary>
        public string? PhotoProfilUrl { get; set; }

        public string? QuestionsJson { get; set; }
    }
}