using Recrutement_api.Services.Interfaces;
using Recrutement_api.DTOs.Expert;

namespace Recrutement_api.Services.Expert.Implementations
{
    public class ExpertFonctionsService : IExpertService
    {
        public Task<List<OffreAssigneeDto>> GetOffresAssigneesAsync(Guid expertId)
        {
            return Task.FromResult(new List<OffreAssigneeDto>());
        }

        public Task<List<CandidatureExpertDto>> GetCandidaturesAsync(Guid expertId, Guid? filtreOffreId = null, string filtreStatut = null)
        {
            return Task.FromResult(new List<CandidatureExpertDto>());
        }

        public Task<CandidatureExpertDto> GetCandidatureDetailAsync(Guid expertId, Guid candidatureId)
        {
            return Task.FromResult(new CandidatureExpertDto());
        }

        public Task<AvisExpertDetailDto> SoumettreAvisAsync(Guid expertId, SoumettreAvisDto dto)
        {
            return Task.FromResult(new AvisExpertDetailDto());
        }
        public Task<ProfilExpertDto> GetProfilAsync(Guid expertId)
        {
            return Task.FromResult(new ProfilExpertDto());
        }
        public Task<ProfilExpertDto> UpdateProfilAsync(Guid expertId, UpdateProfilExpertDto dto)
        {
            return Task.FromResult(new ProfilExpertDto());
        }

    }
}