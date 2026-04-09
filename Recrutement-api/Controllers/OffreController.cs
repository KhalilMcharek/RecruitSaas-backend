using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Controllers
{
    [Route("api/offres")]
    [ApiController]
    [Authorize]
    public class OffreController : ControllerBase
    {
        private readonly IOffreService _offreService;

        public OffreController(IOffreService offreService)
        {
            _offreService = offreService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOffre([FromBody] OffreCreateDto dto)
        {
            try
            {
                var result = await _offreService.CreerOffreAsync(dto);
                return CreatedAtAction(nameof(CreateOffre), new { id = result.Id }, result);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        /* [HttpPut("{offreId}/formulaire")]
         public async Task<IActionResult> ConfigurerFormulaire(Guid offreId, [FromBody] FormulaireConfigDto dto)
         {
             var offre = await _offreService.ConfigurerFormulaireAsync(offreId, dto);
             return Ok(offre);
         }*/
        [HttpGet("{offreId}")]
        public async Task<IActionResult> GetOffre(Guid offreId)
        {
            try
            {
                var offre = await _offreService.ObtenirOffreParIdAsync(offreId);
                return Ok(offre);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetOffres(
                 [FromQuery] Guid? entrepriseId,
                 [FromQuery] string? search,
                 [FromQuery] string? filter)
        {
            var offres = await _offreService.ObtenirOffresParTenantAsync(entrepriseId, search, filter);
            return Ok(offres);
        }

        [HttpPatch("{offreId}/publication")]
        public async Task<IActionResult> ChangerPublication(Guid offreId, [FromQuery] bool publier)
        {
            var offre = await _offreService.ChangerStatutPublicationAsync(offreId, publier);
            return Ok(offre);
        }

        [HttpPatch("{offreId}/public-link")]
        public async Task<IActionResult> TogglePublicLink(Guid offreId, [FromQuery] bool enabled, [FromQuery] DateTime? expiresAt = null)
        {
            var offre = await _offreService.TogglePublicLinkAsync(offreId, enabled, expiresAt);
            return Ok(offre);
        }

        [HttpPost("{offreId}/regenerate-token")]
        public async Task<IActionResult> RegenerateToken(Guid offreId)
        {
            var offre = await _offreService.RegeneratePublicTokenAsync(offreId);
            return Ok(offre);
        }

        [HttpGet("public/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicOffre(string token)
        {
            try
            {
                var offre = await _offreService.GetPublicOffreByTokenAsync(token);
                return Ok(offre);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
        [HttpPost("{offreId}/experts")]
        public async Task<IActionResult> AssignerExperts(Guid offreId, [FromBody] AssignationExpertDto dto)
        {
            var result = await _offreService.AssignerExpertsAsync(offreId, dto);
            return Ok(result);
        }

        [HttpGet("{offreId}/experts/search")]
        public async Task<IActionResult> RechercherExperts(Guid offreId, [FromQuery] string? search)
        {
            var experts = await _offreService.RechercherExpertsAsync(offreId, search);
            return Ok(experts);
        }
        [HttpPut("{offreId}")]
        public async Task<IActionResult> ModifierOffre(Guid offreId, [FromBody] OffreUpdateDto dto)
        {
            var offre = await _offreService.ModifierOffreAsync(offreId, dto);
            return Ok(offre);
        }
        [HttpDelete("{offreId}/experts/{expertId}")]
        public async Task<IActionResult> SupprimerAssignationExpert(Guid offreId, Guid expertId)
        {
            await _offreService.SupprimerAssignationExpertAsync(offreId, expertId);
            return NoContent();
        }

        [HttpDelete("{offreId}")]
        public async Task<IActionResult> SupprimerOffre(Guid offreId)
        {
            await _offreService.SupprimerOffreAsync(offreId);
            return NoContent();
        }
        [HttpPost("{offreId}/formulaire")]
        public async Task<IActionResult> InitialiserFormulaire(Guid offreId)
        {
            var formulaire = await _offreService.InitialiserFormulaireAsync(offreId);
            return Ok(formulaire);
        }

        [HttpPost("{offreId}/champs")]
        public async Task<IActionResult> AjouterChamps(Guid offreId, [FromBody] FormulaireConfigDto request)
        {
            var champs = await _offreService.AjouterChampsAsync(offreId, request.Champs);
            return Ok(champs);
        }
        [HttpPut("champs/{champId}")]
        public async Task<IActionResult> ModifierChamp(Guid champId, [FromBody] ChampPersonnaliseDto dto)
        {
            var champ = await _offreService.ModifierChampAsync(champId, dto);
            return Ok(champ);
        }

        [HttpDelete("champs/{champId}")]
        public async Task<IActionResult> SupprimerChamp(Guid champId)
        {
            await _offreService.SupprimerChampAsync(champId);
            return NoContent();
        }

        [HttpPut("formulaires/{id}/ordre")]
        public async Task<IActionResult> ModifierOrdreChamps(
      Guid id,
      [FromBody] List<ModifierOrdreChampDto> dtos)
        {
            await _offreService.ModifierOrdreChampsAsync(id, dtos);

            return NoContent();
        }
        [HttpGet("entreprises")]
        public async Task<IActionResult> GetEntreprises()
        {
            var entreprises = await _offreService.GetTenantEntreprisesAsync();
            return Ok(entreprises);
        }
    }
}