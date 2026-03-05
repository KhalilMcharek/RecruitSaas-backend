using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CandidatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CandidatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET: api/candidat/formulaire/{offreId}
        // Récupérer le formulaire lié à l’offre
        // ============================================
        [HttpGet("formulaire/{offreId}")]
        public async Task<IActionResult> GetFormulaire(Guid offreId)
        {
            var formulaire = await _context.FormulairesCandidature
                .Where(f => f.OffreId == offreId)
                .FirstOrDefaultAsync();

            if (formulaire == null)
                return NotFound();

            return Ok(formulaire);
        }

        // ============================================
        // POST: api/candidat/postuler
        // Envoyer une candidature
        // ============================================
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler([FromBody] PostulerDTO dto)
        {
            var offreExiste = await _context.OffresEmploi
                .AnyAsync(o => o.Id == dto.OffreId && o.EstPublie);

            if (!offreExiste)
                return BadRequest("Offre invalide");

            var candidature = new Candidature
            {
                Id = Guid.NewGuid(),
                OffreId = dto.OffreId,
                CandidatId = dto.CandidatId,
                CvUrl = dto.CvUrl,
                Statut = "Nouvelle",
                CreeLe = DateTime.UtcNow
            };

            _context.Candidatures.Add(candidature);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Candidature envoyée avec succès",
                candidature.Id
            });
        }
    }
}