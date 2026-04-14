using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Recrutement_api.DTOs.Expert;

namespace Recrutement_api.Services.Interfaces
{
    public interface IExpertService
    {
        /// <summary>Toutes les offres auxquelles l'expert est assigné, avec le nombre de candidatures.</summary>
        Task<List<OffreAssigneeDto>> GetOffresAssigneesAsync(Guid expertId);

        /// <summary>
        /// Toutes les candidatures pour les offres assignées à l'expert.
        /// Filtres optionnels : offreId, statut.
        /// </summary>
        Task<List<CandidatureExpertDto>> GetCandidaturesAsync(
            Guid expertId,
            Guid? filtreOffreId = null,
            string filtreStatut = null);

        /// <summary>Détail d'une candidature — vérifie que l'expert est bien assigné à l'offre.</summary>
        Task<CandidatureExpertDto> GetCandidatureDetailAsync(Guid expertId, Guid candidatureId);

        /// <summary>Soumettre ou mettre à jour un avis (upsert par expertId + candidatureId).</summary>
        Task<AvisExpertDetailDto> SoumettreAvisAsync(Guid expertId, SoumettreAvisDto dto);

        Task<ProfilExpertDto> GetProfilAsync(Guid expertId);
        Task<ProfilExpertDto> UpdateProfilAsync(Guid expertId, UpdateProfilExpertDto dto);
    }
    
} 