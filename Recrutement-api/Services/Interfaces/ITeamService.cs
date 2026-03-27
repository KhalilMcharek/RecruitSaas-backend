using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;

namespace Recrutement_api.Services.Interfaces
{
    public interface ITeamService
    {
        Task<List<ExpertDto>> GetExpertsAsync(Guid? entrepriseId, string? search);
        Task<ExpertDto> GetExpertByIdAsync(Guid id);
        Task<ExpertDto> CreateExpertAsync(ExpertCreateDto dto);
        Task<ExpertDto> UpdateExpertAsync(Guid id, ExpertUpdateDto dto);
        Task DeleteExpertAsync(Guid id);
        Task<List<Entreprise>> GetEntreprisesAsync();
    }
}
