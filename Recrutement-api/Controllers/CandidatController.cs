using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

       [HttpPost("postuler")]
public async Task<IActionResult> Postuler(
    [FromForm] Guid offreId,
    [FromForm] Guid candidatId,
    [FromForm] IFormFile cv)
{
    try
    {
        if (cv == null || cv.Length == 0)
            return BadRequest(new { message = "Le CV est obligatoire." });

        var offre = await _context.OffresEmploi
            .FirstOrDefaultAsync(o => o.Id == offreId && o.EstPublie);

        if (offre == null)
            return BadRequest(new { message = "Offre invalide ou non publiée." });

        // ✅ Pas de vérification candidat — on fait confiance à l'ID passé
        var dejaPostule = await _context.Candidatures
            .AnyAsync(c => c.OffreId == offreId && c.CandidatId == candidatId);

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
            CandidatId = candidatId,
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
        _logger.LogError("❌ Erreur: {Error}", inner);
        return StatusCode(500, new { message = inner });
    }
}}}