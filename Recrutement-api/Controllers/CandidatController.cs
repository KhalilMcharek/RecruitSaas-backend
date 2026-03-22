using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;
using System.Security.Claims;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // ✅ Protège tout le controller — 401 si pas de token valide
    public class CandidatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CandidatController> _logger;

        public CandidatController(
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            ILogger<CandidatController> logger)
        {
            _context = context;
            _logger = logger;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : extrait le CandidatId depuis le JWT (côté serveur)
        // ─────────────────────────────────────────────────────────────
        private Guid? GetCandidatIdFromToken()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("candidatId")
                     ?? User.FindFirstValue("nameid")
                     ?? User.FindFirstValue("sub");

            if (claim == null) return null;
            if (Guid.TryParse(claim, out var id)) return id;
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/candidat/formulaire/{offreId}
        // ─────────────────────────────────────────────────────────────
     
        // ─────────────────────────────────────────────────────────────
        // POST api/candidat/postuler
        // ─────────────────────────────────────────────────────────────
        [HttpPost("postuler")]
        public async Task<IActionResult> Postuler(
            [FromForm] Guid offreId,
            [FromForm] IFormFile cv,
            [FromForm] string? fullName,
            [FromForm] string? email)
        {
            try
            {
                // ✅ ID extrait du JWT — plus besoin de le passer depuis le frontend
                var candidatId = GetCandidatIdFromToken();
                if (candidatId == null)
                    return Unauthorized(new { message = "Session invalide. Veuillez vous reconnecter." });

                if (cv == null || cv.Length == 0)
                    return BadRequest(new { message = "Le CV est obligatoire." });

                var offre = await _context.OffresEmploi
                    .FirstOrDefaultAsync(o => o.Id == offreId && o.EstPublie);

                if (offre == null)
                    return BadRequest(new { message = "Offre invalide ou non publiée." });

                var dejaPostule = await _context.Candidatures
                    .AnyAsync(c => c.OffreId == offreId && c.CandidatId == candidatId.Value);

                if (dejaPostule)
                    return Conflict(new { message = "Vous avez déjà postulé à cette offre." });

                // Upload Cloudinary
                string cvUrl;
                using (var stream = cv.OpenReadStream())
                {
                    var uploadParams = new RawUploadParams
                    {
                        File = new FileDescription(cv.FileName, stream),
                        Folder = "cvs",
                        PublicId = $"cvs/{candidatId}_{offreId}_{DateTime.UtcNow.Ticks}"
                    };

                    var result = await _cloudinary.UploadAsync(uploadParams);

                    if (result.Error != null)
                        return StatusCode(500, new { message = "Erreur Cloudinary : " + result.Error.Message });

                    cvUrl = result.SecureUrl.ToString();
                }

                var candidature = new Candidature
                {
                    Id = Guid.NewGuid(),
                    OffreId = offreId,
                    CandidatId = candidatId.Value,
                    CvUrl = cvUrl,
                    Statut = "Nouvelle",
                    CreeLe = DateTime.UtcNow
                };

                _context.Candidatures.Add(candidature);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Candidature envoyée avec succès",
                    candidatureId = candidature.Id,
                    cvUrl
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.InnerException?.Message
                         ?? ex.InnerException?.Message
                         ?? ex.Message;

                _logger.LogError("❌ Erreur postuler: {Error}", inner);
                return StatusCode(500, new { message = inner });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/candidat/mes-candidatures
        // ─────────────────────────────────────────────────────────────
        [HttpGet("mes-candidatures")]
        public async Task<IActionResult> MesCandidatures()
        {
            var candidatId = GetCandidatIdFromToken();
            if (candidatId == null)
                return Unauthorized(new { message = "Session invalide." });

            var candidatures = await _context.Candidatures
                .Where(c => c.CandidatId == candidatId.Value)
                .Include(c => c.Offre)
                .OrderByDescending(c => c.CreeLe)
                .Select(c => new
                {
                    c.Id,
                    c.Statut,
                    c.CvUrl,
                    c.CreeLe,
                    Offre = new
                    {
                        c.Offre.Id,
                        c.Offre.Titre,
                        c.Offre.Localisation
                    }
                })
                .ToListAsync();

            return Ok(candidatures);
        }
    }
}