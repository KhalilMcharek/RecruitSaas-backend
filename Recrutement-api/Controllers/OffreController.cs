using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
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
        private readonly ApplicationDbContext _context;

        public OffreController(IOffreService offreService, ApplicationDbContext context)
        {
            _offreService = offreService;
            _context = context;
        }

        // ──────────────────────────────────────────────
        // PUBLIC — sans token (candidats)
        // ──────────────────────────────────────────────

        /// GET /api/offres — liste publique des offres publiées
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffres()
        {
            var offres = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.EstPublie)
                .Select(o => new OffreListDTO
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .OrderByDescending(o => o.CreeLe)
                .ToListAsync();

            return Ok(offres);
        }

        /// GET /api/offres/{id} — détail public d'une offre publiée
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOffreDetail(Guid id)
        {
            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.Id == id && o.EstPublie)
                .Select(o => new OffreDetailDTO
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Description   = o.Description,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    EntrepriseNom = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .FirstOrDefaultAsync();

            if (offre == null)
                return NotFound(new { message = "Offre introuvable ou non publiée." });

            return Ok(offre);
        }

        // ──────────────────────────────────────────────
        // ADMIN / RECRUTEUR — token requis
        // ──────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> CreateOffre([FromBody] OffreCreateDto dto)
        {
            try
            {
                var result = await _offreService.CreerOffreAsync(dto);
                return CreatedAtAction(nameof(GetOffreDetail), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetOffresAdmin(
            [FromQuery] Guid?   entrepriseId,
            [FromQuery] string? search,
            [FromQuery] string? filter)
        {
            var offres = await _offreService.ObtenirOffresParTenantAsync(
                entrepriseId, search ?? "", filter ?? "");
            return Ok(offres);
        }

        [HttpPatch("{offreId:guid}/publication")]
        public async Task<IActionResult> ChangerPublication(Guid offreId, [FromQuery] bool publier)
        {
            var offre = await _offreService.ChangerStatutPublicationAsync(offreId, publier);
            return Ok(offre);
        }

        [HttpPost("{offreId:guid}/experts")]
        public async Task<IActionResult> AssignerExperts(Guid offreId, [FromBody] AssignationExpertDto dto)
        {
            var result = await _offreService.AssignerExpertsAsync(offreId, dto);
            return Ok(result);
        }

        [HttpGet("{offreId:guid}/experts/search")]
        public async Task<IActionResult> RechercherExperts(Guid offreId, [FromQuery] string? search)
        {
            var experts = await _offreService.RechercherExpertsAsync(offreId, search);
            return Ok(experts);
        }

        [HttpPut("{offreId:guid}")]
        public async Task<IActionResult> ModifierOffre(Guid offreId, [FromBody] OffreUpdateDto dto)
        {
            var offre = await _offreService.ModifierOffreAsync(offreId, dto);
            return Ok(offre);
        }

        [HttpDelete("{offreId:guid}/experts/{expertId:guid}")]
        public async Task<IActionResult> SupprimerAssignationExpert(Guid offreId, Guid expertId)
        {
            await _offreService.SupprimerAssignationExpertAsync(offreId, expertId);
            return NoContent();
        }

        [HttpDelete("{offreId:guid}")]
        public async Task<IActionResult> SupprimerOffre(Guid offreId)
        {
            await _offreService.SupprimerOffreAsync(offreId);
            return NoContent();
        }

        [HttpPost("{offreId:guid}/formulaire")]
        public async Task<IActionResult> InitialiserFormulaire(Guid offreId)
        {
            var formulaire = await _offreService.InitialiserFormulaireAsync(offreId);
            return Ok(formulaire);
        }

        [HttpPost("{offreId:guid}/champs")]
        public async Task<IActionResult> AjouterChamps(Guid offreId, [FromBody] FormulaireConfigDto request)
        {
            var champs = await _offreService.AjouterChampsAsync(offreId, request.Champs);
            return Ok(champs);
        }

        [HttpPut("champs/{champId:guid}")]
        public async Task<IActionResult> ModifierChamp(Guid champId, [FromBody] ChampPersonnaliseDto dto)
        {
            var champ = await _offreService.ModifierChampAsync(champId, dto);
            return Ok(champ);
        }

        [HttpDelete("champs/{champId:guid}")]
        public async Task<IActionResult> SupprimerChamp(Guid champId)
        {
            await _offreService.SupprimerChampAsync(champId);
            return NoContent();
        }

        [HttpPut("formulaires/{id:guid}/ordre")]
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