using Recrutement_api.DTOs.Candidat;
using Recrutement_api.DTOs.Offre;

namespace Recrutement_api.Services.CandidatServices
{
    public interface ICandidatService
    {
        Task<object> GetFormulaireAsync(Guid offreId);
Task<bool> HasAlreadyAppliedAsync(Guid userId, Guid offreId);
    Task<bool> CancelCandidatureAsync(Guid userId, Guid offreId); // ← Nouvelle méthode

        
        Task<PostulerResultDto> PostulerAsync(Guid userId, PostulerDto dto);

        Task<IEnumerable<object>> GetMesCandidaturesAsync(Guid candidatId);

        Task<IEnumerable<OffreResponseDto>> GetOffresAsync();
        Task<OffreResponseDto?> GetOffreDetailAsync(Guid offreId);

        Task<object?> GetCandidatureByIdAsync(Guid candidatureId);
        Task<bool> UpdateCandidatureStatutAsync(Guid candidatureId, string statut);
    }
}
