using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;
using System.Security.Claims;

namespace Recrutement_api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SavedJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISavedJobService _savedJobService;
        private readonly ILogger<SavedJobsController> _logger;

        public SavedJobsController(
            ApplicationDbContext context,
            ISavedJobService savedJobService,
            ILogger<SavedJobsController> logger)
        {
            _context = context;
            _savedJobService = savedJobService;
            _logger = logger;
        }

        // 🔍 Helper : Extraire l'UtilisateurId depuis le token JWT
        private Guid? GetUtilisateurIdFromToken()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("userId")?.Value
                       ?? User.FindFirst("id")?.Value;

            if (Guid.TryParse(idClaim, out var id))
            {
                return id;
            }
            return null;
        }

        // 🔍 Helper : Trouver le Candidat via son UtilisateurId
        private async Task<Candidat?> FindCandidatByUtilisateurIdAsync(Guid utilisateurId)
        {
            return await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == utilisateurId);
        }

        // GET: api/savedjobs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSavedJobs()
        {
            try
            {
                var utilisateurId = GetUtilisateurIdFromToken();
                if (utilisateurId == null)
                    return Unauthorized(new { message = "Utilisateur non authentifié" });

                // ✅ Trouver le Candidat via UtilisateurId
                var candidat = await FindCandidatByUtilisateurIdAsync(utilisateurId.Value);
                if (candidat == null)
                    return BadRequest(new { message = "Profil candidat non trouvé" });

                _logger.LogInformation("✅ Récupération des offres pour Candidat {CandidatId} (UtilisateurId: {UtilisateurId})", 
                    candidat.Id, utilisateurId);

                var savedJobs = await _savedJobService.GetSavedJobsAsync(candidat.Id);

                var result = savedJobs.Select(sj => new
                {
                    id = sj.Id,
                    offreId = sj.OffreId,
                    savedAt = sj.SavedAt,
                    offre = new
                    {
                        id = sj.OffreId,
                        titre = sj.Titre,
                        nomEntreprise = sj.NomEntreprise,
                        localisation = sj.Localisation,
                        typeContrat = sj.TypeContrat,
                        logoUrl = sj.LogoUrl,
                        creeLe = sj.CreeLe,
                        teletravail = false
                    }
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERREUR GetSavedJobs: {Message}", ex.Message);
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // POST: api/savedjobs - CORRECTION CLÉ ICI 🔑
        [HttpPost]
        public async Task<IActionResult> SaveJob([FromBody] SaveJobRequest request)
        {
            try
            {
                _logger.LogInformation("📥 Requête POST /savedjobs: OffreId={OffreId}", request.OffreId);

                // 1️⃣ Extraire l'UtilisateurId depuis le token
                var utilisateurId = GetUtilisateurIdFromToken();
                if (utilisateurId == null)
                {
                    _logger.LogWarning("❌ UtilisateurId non trouvé dans le token");
                    return Unauthorized(new { message = "Utilisateur non authentifié" });
                }

                // 2️⃣ ✅ TROUVER le Candidat via UtilisateurId
                var candidat = await FindCandidatByUtilisateurIdAsync(utilisateurId.Value);
                if (candidat == null)
                {
                    _logger.LogWarning("❌ Aucun Candidat trouvé pour UtilisateurId={UtilisateurId}", utilisateurId);
                    return BadRequest(new { 
                        message = "Profil candidat non trouvé. Veuillez compléter votre inscription.",
                        utilisateurId 
                    });
                }

                _logger.LogInformation("✅ Candidat trouvé: Candidat.Id={CandidatId}, UtilisateurId={UtilisateurId}", 
                    candidat.Id, candidat.UtilisateurId);

                // 3️⃣ Vérifier que l'offre existe
                var offreExists = await _context.OffresEmploi.AnyAsync(o => o.Id == request.OffreId);
                if (!offreExists)
                {
                    _logger.LogWarning("❌ Offre {OffreId} non trouvée", request.OffreId);
                    return NotFound(new { message = "Offre non trouvée" });
                }

                // 4️⃣ Vérifier si déjà sauvegardé (utiliser Candidat.Id !)
                var alreadySaved = await _context.SavedJobs
                    .AnyAsync(s => s.CandidatId == candidat.Id && s.OffreId == request.OffreId);
                
                if (alreadySaved)
                {
                    _logger.LogInformation("ℹ️ Offre déjà sauvegardée pour ce candidat");
                    return Ok(new { message = "Déjà sauvegardé" });
                }

                // 5️⃣ ✅ CRÉER l'enregistrement avec Candidat.Id (PK), PAS UtilisateurId !
                var savedJob = new SavedJob
                {
                    Id = Guid.NewGuid(),
                    CandidatId = candidat.Id,  // 🔑 CLÉ : utiliser l'ID de la table Candidats (PK)
                    OffreId = request.OffreId,
                    SavedAt = DateTime.UtcNow
                };

                _logger.LogInformation("💾 Insertion dans SavedJobs: CandidatId={CandidatId}, OffreId={OffreId}", 
                    savedJob.CandidatId, savedJob.OffreId);
                
                _context.SavedJobs.Add(savedJob);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Offre sauvegardée avec succès, SavedJob.Id={SavedJobId}", savedJob.Id);

                return Ok(new 
                { 
                    message = "Offre sauvegardée avec succès",
                    savedJobId = savedJob.Id,
                    offreId = savedJob.OffreId,
                    candidatId = savedJob.CandidatId  // Pour debug
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ ERREUR BASE DE DONNÉES: {Message}", dbEx.Message);
                
                // Gestion explicite des violations de clé étrangère
                if (dbEx.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23503")
                {
                    _logger.LogError("❌ Violation contrainte FK: {Constraint}", pgEx.ConstraintName);
                    return BadRequest(new { 
                        message = "Référence invalide : vérifiez que le candidat et l'offre existent",
                        constraint = pgEx.ConstraintName,
                        detail = "Include Error Detail dans la connection string pour plus d'infos"
                    });
                }
                
                return StatusCode(500, new { message = "Erreur base de données", error = dbEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERREUR CRITIQUE SaveJob: {Message}", ex.Message);
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // DELETE: api/savedjobs/{offreId}
        [HttpDelete("{offreId}")]
        public async Task<IActionResult> UnsaveJob(Guid offreId)
        {
            try
            {
                var utilisateurId = GetUtilisateurIdFromToken();
                if (utilisateurId == null)
                    return Unauthorized(new { message = "Utilisateur non authentifié" });

                var candidat = await FindCandidatByUtilisateurIdAsync(utilisateurId.Value);
                if (candidat == null)
                    return BadRequest(new { message = "Profil candidat non trouvé" });

                var savedJob = await _context.SavedJobs
                    .FirstOrDefaultAsync(s => s.CandidatId == candidat.Id && s.OffreId == offreId);

                if (savedJob == null)
                    return NotFound(new { message = "Offre non sauvegardée" });

                _context.SavedJobs.Remove(savedJob);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERREUR UnsaveJob: {Message}", ex.Message);
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }
    }

    public class SaveJobRequest
    {
        public Guid OffreId { get; set; }
    }
}