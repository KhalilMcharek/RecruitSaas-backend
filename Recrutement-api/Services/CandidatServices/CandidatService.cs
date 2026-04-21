using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Candidat;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Models;

namespace Recrutement_api.Services.CandidatServices
{
    public class AlreadyAppliedException : Exception
    {
        public AlreadyAppliedException() : base("Vous avez déjà postulé à cette offre.") { }
    }

    public class CandidatService : ICandidatService
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CandidatService> _logger;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "webp", "bmp",
            "tiff", "tif", "svg", "ico", "avif", "heic", "heif"
        };

        public CandidatService(
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            ILogger<CandidatService> logger)
        {
            _context = context;
            _logger  = logger;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────────
        // GET formulaire
        // ─────────────────────────────────────────────────────────────
        public async Task<object> GetFormulaireAsync(Guid offreId)
        {
            var formulaire = await _context.FormulairesCandidature
                .Include(f => f.ChampsPersonnalises)
                .FirstOrDefaultAsync(f => f.OffreId == offreId);

            if (formulaire == null)
                return new { champsPersonnalises = new List<object>() };

            return new
            {
                id      = formulaire.Id,
                offreId = formulaire.OffreId,
                champsPersonnalises = formulaire.ChampsPersonnalises
                    .OrderBy(c => c.Ordre)
                    .Select(c => new
                    {
                        id             = c.Id,
                        nom            = c.Nom,
                        question       = c.Question,
                        type           = c.Type,
                        estObligatoire = c.EstObligatoire,
                        optionsJson    = c.OptionsJson,
                        ordre          = c.Ordre
                    })
                    .ToList()
            };
        }

        // ─────────────────────────────────────────────────────────────
        // POST postuler
        // ─────────────────────────────────────────────────────────────
        public async Task<PostulerResultDto> PostulerAsync(Guid userId, PostulerDto dto)
        {
            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId)
                ?? throw new Exception("Profil candidat introuvable.");

            if (dto.Cv == null || dto.Cv.Length == 0)
                throw new Exception("Le CV est obligatoire.");

            var offre = await _context.OffresEmploi
                .FirstOrDefaultAsync(o => o.Id == dto.OffreId && o.EstPublie)
                ?? throw new Exception("Offre invalide ou non publiée.");

            var dejaPostule = await _context.Candidatures
                .AnyAsync(c => c.OffreId == dto.OffreId && c.CandidatId == candidat.Id);

            if (dejaPostule)
                throw new AlreadyAppliedException();

            string cvUrl = await UploadToCloudinaryAsync(dto.Cv);

            var candidature = new Candidature
            {
                Id                  = Guid.NewGuid(),
                OffreId             = dto.OffreId,
                CandidatId          = candidat.Id,
                CvUrl               = cvUrl,
                Statut              = "Nouvelle",
                CreeLe              = DateTime.UtcNow,
                FormulaireResponses = BuildFormulaireResponsesJson(dto)
            };

            _context.Candidatures.Add(candidature);
            await _context.SaveChangesAsync();

            return new PostulerResultDto
            {
                Message       = "Candidature envoyée avec succès.",
                CandidatureId = candidature.Id,
                CvUrl         = cvUrl
            };
        }

        // ─────────────────────────────────────────────────────────────
        // Upload Cloudinary
        // ─────────────────────────────────────────────────────────────
        private async Task<string> UploadToCloudinaryAsync(IFormFile file)
        {
            var ext      = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            var publicId = "cv_" + Guid.NewGuid().ToString("N");

            using var stream = file.OpenReadStream();

            if (ImageExtensions.Contains(ext))
            {
                var result = await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File       = new FileDescription(file.FileName, stream),
                    PublicId   = publicId,
                    AccessMode = "public",
                    Type       = "upload"
                });

                if (result.Error != null)
                    throw new Exception($"Cloudinary image upload error: {result.Error.Message}");

                return result.SecureUrl.ToString();
            }
            else
            {
                var result = await _cloudinary.UploadAsync(new RawUploadParams
                {
                    File       = new FileDescription(file.FileName, stream),
                    PublicId   = publicId,
                    AccessMode = "public",
                    Type       = "upload"
                });

                if (result.Error != null)
                    throw new Exception($"Cloudinary raw upload error: {result.Error.Message}");

                return result.SecureUrl.ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GET mes-candidatures
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<object>> GetMesCandidaturesAsync(Guid candidatId)
        {
            return await _context.Candidatures
                .Where(c => c.CandidatId == candidatId)
                .Include(c => c.Offre)
                .OrderByDescending(c => c.CreeLe)
                .Select(c => (object)new
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
        }

        // ─────────────────────────────────────────────────────────────
        // GET offres (public)
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<OffreResponseDto>> GetOffresAsync()
        {
            return await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.EstPublie)
                .OrderByDescending(o => o.CreeLe)
                .Select(o => new OffreResponseDto
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    NomEntreprise = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .ToListAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // GET offres/{id} (public)
        // ─────────────────────────────────────────────────────────────
        public async Task<OffreResponseDto?> GetOffreDetailAsync(Guid offreId)
        {
            return await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Where(o => o.Id == offreId && o.EstPublie)
                .Select(o => new OffreResponseDto
                {
                    Id            = o.Id,
                    Titre         = o.Titre,
                    Description   = o.Description,
                    Localisation  = o.Localisation,
                    CreeLe        = o.CreeLe,
                    NomEntreprise = o.Entreprise != null ? o.Entreprise.Nom : "—"
                })
                .FirstOrDefaultAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // Helper : build formulaire JSON
        // ─────────────────────────────────────────────────────────────
        private string BuildFormulaireResponsesJson(PostulerDto dto)
        {
            var responses = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(dto.FullName))     responses["fullName"]     = dto.FullName;
            if (!string.IsNullOrEmpty(dto.Email))        responses["email"]        = dto.Email;
            if (!string.IsNullOrEmpty(dto.PortfolioUrl)) responses["portfolioUrl"] = dto.PortfolioUrl;
            if (!string.IsNullOrEmpty(dto.Motivation))   responses["motivation"]   = dto.Motivation;

            if (!string.IsNullOrWhiteSpace(dto.ChampsPersonnalises))
            {
                try
                {
                    var extra = System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, object>>(dto.ChampsPersonnalises);

                    if (extra != null)
                        foreach (var kv in extra)
                            responses[kv.Key] = kv.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("JSON champsPersonnalises invalide : {Message}", ex.Message);
                }
            }

            return responses.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(responses)
                : "{}";
        }

        // ─────────────────────────────────────────────────────────────
        // HasAlreadyAppliedAsync
        // ─────────────────────────────────────────────────────────────
        public async Task<bool> HasAlreadyAppliedAsync(Guid userId, Guid offreId)
        {
            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId);

            if (candidat == null) return false;

            return await _context.Candidatures
                .AnyAsync(c => c.CandidatId == candidat.Id && c.OffreId == offreId);
        }

        // ─────────────────────────────────────────────────────────────
        // ★★ CancelCandidatureAsync ← NOUVELLE MÉTHODE ★★
        // ─────────────────────────────────────────────────────────────
        public async Task<bool> CancelCandidatureAsync(Guid userId, Guid offreId)
        {
            // 1. Récupérer le candidat lié à l'utilisateur
            var candidat = await _context.Candidats
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId);

            if (candidat == null) return false;

            // 2. Trouver la candidature correspondante
            var candidature = await _context.Candidatures
                .FirstOrDefaultAsync(c => c.CandidatId == candidat.Id && c.OffreId == offreId);

            if (candidature == null) return false;

            // 3. Supprimer et sauvegarder
            _context.Candidatures.Remove(candidature);
            await _context.SaveChangesAsync();

            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // GET candidature by id
        // ─────────────────────────────────────────────────────────────
        public async Task<object?> GetCandidatureByIdAsync(Guid candidatureId)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                    .ThenInclude(c => c.Utilisateur)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null) return null;

            return new
            {
                candidature.Id,
                candidature.Statut,
                candidature.CvUrl,
                candidature.CreeLe,
                candidature.FormulaireResponses,
                titreOffre = candidature.Offre != null ? candidature.Offre.Titre : null,
                candidatId = candidature.Candidat != null ? candidature.Candidat.UtilisateurId : (Guid?)null,
            };
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH candidature statut
        // ─────────────────────────────────────────────────────────────
        public async Task<bool> UpdateCandidatureStatutAsync(Guid candidatureId, string statut)
        {
            var candidature = await _context.Candidatures.FindAsync(candidatureId);
            if (candidature == null) return false;

            candidature.Statut = statut;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}