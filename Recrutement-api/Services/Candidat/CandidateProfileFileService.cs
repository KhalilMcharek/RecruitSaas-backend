using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Candidate;
using Recrutement_api.Models;
using System.Text.Json;

namespace Recrutement_api.Services.CandidateServices
{
    public class CandidateProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CandidateProfileService> _logger;

        public CandidateProfileService(
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            ILogger<CandidateProfileService> logger)
        {
            _context = context;
            _logger  = logger;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret);
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────
        // GET profile
        // ─────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message, CandidateProfileDto? Data)>
            GetProfileAsync(Guid userId)
        {
            var candidat = await _context.Candidats
                .Include(c => c.Utilisateur)
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId);

            if (candidat == null)
                return (false, "Candidat introuvable", null);

            var profile = await _context.CandidateProfiles
                .FirstOrDefaultAsync(p => p.CandidatId == candidat.Id);

            var dto = BuildDto(candidat, profile);
            return (true, "OK", dto);
        }

        // ─────────────────────────────────────────────────────────
        // PUT profile
        // ─────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message, CandidateProfileDto? Data)>
            UpdateProfileAsync(Guid userId, CandidateProfileDto dto, IFormFile? avatar)
        {
            var candidat = await _context.Candidats
                .Include(c => c.Utilisateur)
                .FirstOrDefaultAsync(c => c.UtilisateurId == userId);

            if (candidat == null)
                return (false, "Candidat introuvable", null);

            // Récupère ou crée le profil
            var profile = await _context.CandidateProfiles
                .FirstOrDefaultAsync(p => p.CandidatId == candidat.Id);

            if (profile == null)
            {
                profile = new CandidateProfile { CandidatId = candidat.Id };
                _context.CandidateProfiles.Add(profile);
            }

            // ── Upload avatar sur Cloudinary ──────────────────────
            if (avatar != null && avatar.Length > 0)
            {
                if (avatar.Length > 5 * 1024 * 1024)
                    return (false, "L'avatar ne doit pas dépasser 5 Mo", null);

                var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
                if (!allowed.Contains(ext))
                    return (false, "Format non supporté (jpg, png, webp, svg)", null);

                var (url, error) = await UploadImageAsync(avatar, "candidate-avatars");
                if (error != null)
                    return (false, $"Erreur upload avatar : {error}", null);

                profile.AvatarUrl = url;
            }

            // ── Mise à jour des champs ────────────────────────────
            profile.Phone        = dto.Phone;
            profile.Location     = dto.Location;
            profile.Bio          = dto.Bio;
            profile.Seeking      = dto.Seeking;
            profile.Education    = dto.Education;
            profile.FieldOfStudy = dto.FieldOfStudy;
            profile.Experience   = dto.Experience;
            profile.Availability = dto.Availability;
            profile.SkillsJson   = dto.Skills != null
                                       ? JsonSerializer.Serialize(dto.Skills)
                                       : "[]";
            profile.Linkedin     = dto.Linkedin;
            profile.Github       = dto.Github;
            profile.PortfolioUrl = dto.PortfolioUrl;
            profile.UpdatedAt    = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Profil candidat mis à jour: {id}", candidat.Id);
            return (true, "Profil enregistré avec succès", BuildDto(candidat, profile));
        }

        // ─────────────────────────────────────────────────────────
        // Helpers privés
        // ─────────────────────────────────────────────────────────
        private static CandidateProfileDto BuildDto(Candidat candidat, CandidateProfile? profile)
        {
            return new CandidateProfileDto
            {
                // Lus depuis Utilisateurs
                FullName = candidat.Utilisateur.Nom   ?? "",
                Email    = candidat.Utilisateur.Email ?? "",

                // Lus depuis CandidateProfile
                Phone        = profile?.Phone,
                Location     = profile?.Location,
                Bio          = profile?.Bio,
                Seeking      = profile?.Seeking ?? "student",
                Education    = profile?.Education,
                FieldOfStudy = profile?.FieldOfStudy,
                Experience   = profile?.Experience,
                Availability = profile?.Availability,
                Skills       = string.IsNullOrEmpty(profile?.SkillsJson)
                                   ? new List<string>()
                                   : JsonSerializer.Deserialize<List<string>>(profile.SkillsJson) ?? new(),
                Linkedin     = profile?.Linkedin,
                Github       = profile?.Github,
                PortfolioUrl = profile?.PortfolioUrl,
                AvatarUrl    = profile?.AvatarUrl,
            };
        }

        private async Task<(string? Url, string? Error)> UploadImageAsync(IFormFile file, string folder)
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File           = new FileDescription(file.FileName, stream),
                Folder         = folder,
                Transformation = new Transformation().Quality("auto")
            };
            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                _logger.LogWarning("Cloudinary error: {msg}", result.Error.Message);
                return (null, result.Error.Message);
            }
            return (result.SecureUrl.ToString(), null);
        }
    }
}