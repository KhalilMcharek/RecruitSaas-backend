using Recrutement_api.DTOs.Offre;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
namespace Recrutement_api.Services.Interfaces
{
    public interface IOffreService
    {
        Task<OffreResponseDto> CreerOffreAsync(OffreCreateDto dto);
        //Task<OffreResponseDto> ConfigurerFormulaireAsync(Guid offreId, FormulaireConfigDto dto);
        Task<OffreResponseDto> ObtenirOffreParIdAsync(Guid offreId);
        Task<List<OffreResponseDto>> ObtenirOffresParTenantAsync(Guid? entrepriseId, string search, string filter);
        Task<OffreResponseDto> ChangerStatutPublicationAsync(Guid offreId, bool publier);
        Task<OffreResponseDto> TogglePublicLinkAsync(Guid offreId, bool enabled, DateTime? expiresAt = null);
        Task<OffreResponseDto> RegeneratePublicTokenAsync(Guid offreId);
        Task<PublicOffreResponseDto> GetPublicOffreByTokenAsync(string token);
        Task<OffreResponseDto> AssignerExpertsAsync(Guid offreId, AssignationExpertDto dto);
        Task<object> RechercherExpertsAsync(Guid offreId, string? search);
        Task <OffreResponseDto> ModifierOffreAsync(Guid offreId, OffreUpdateDto dto);
        Task SupprimerAssignationExpertAsync(Guid offreId, Guid expertId);
        Task SupprimerOffreAsync(Guid offreId);
        Task<FormulaireResponseDto> InitialiserFormulaireAsync(Guid offreId);
        Task<List<ChampPersonnaliseResponseDto>> AjouterChampsAsync(Guid offreId, List<ChampPersonnaliseDto> dtos);
        Task<ChampPersonnaliseResponseDto> ModifierChampAsync(Guid champId, ChampPersonnaliseDto dto);
        Task SupprimerChampAsync(Guid champId);

        Task ModifierOrdreChampsAsync(Guid formulaireId, List<ModifierOrdreChampDto> dtos);
        Task<List<Entreprise>> GetTenantEntreprisesAsync();
    }
}
