using Recrutement_api.DTOs.Entretien;

namespace Recrutement_api.Services.Entretien
{
    public interface IEntretienService
    {
        Task<object> PlanifierAsync(Guid candidatureId, PlanifierDto dto);
        Task<List<object>> GetAllAsync(Guid? offreId, string? statut);
        Task<object?> GetByIdAsync(Guid id);
        Task AnnulerAsync(Guid id);
        Task<object?> GetCreneauxByTokenAsync(string token);
        Task<object?> ConfirmerCreneauAsync(string token, DateTime dateChoisie);
        Task<EntretienRejoindreResult?> RejoindreAsync(string token);
    Task<List<object>> GetEntretiensByCandidatAsync(Guid candidatId);
    }

    public class EntretienRejoindreResult
    {
        public bool EstActif { get; set; }
        public DateTime? DateScheduled { get; set; }
        public string? LienEntretien { get; set; }
        public string? NomCandidat { get; set; }
        public string? TitreOffre { get; set; }
    }
}