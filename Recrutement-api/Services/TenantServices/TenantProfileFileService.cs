using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Tenant;
using Recrutement_api.Models;
using System.Text.Json;

namespace Recrutement_api.Services.TenantServices
{
    public class TenantProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<TenantProfileService> _logger;

        public TenantProfileService(
            ApplicationDbContext context,
            IOptions<CloudinarySettings> cloudinaryConfig,
            ILogger<TenantProfileService> logger)
        {
            _context = context;
            _logger = logger;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<(bool Success, string Message, TenantProfileDto? Data)>
            GetProfileAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Utilisateur)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                return (false, "Tenant introuvable", null);

            var profile = await _context.TenantProfiles
                .FirstOrDefaultAsync(p => p.TenantId == tenantId);

            return (true, "OK", BuildDto(tenant, profile));
        }

        public async Task<(bool Success, string Message, TenantProfileDto? Data)>
            UpdateProfileAsync(Guid tenantId, TenantProfileDto dto, IFormFile? logo)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Utilisateur)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                return (false, "Tenant introuvable", null);

            var profile = await _context.TenantProfiles
                .FirstOrDefaultAsync(p => p.TenantId == tenantId);

            if (profile == null)
            {
                profile = new TenantProfile { TenantId = tenantId };
                _context.TenantProfiles.Add(profile);
            }

            // ── Upload logo ─────────────────────────────────
            if (logo != null && logo.Length > 0)
            {
                if (logo.Length > 5 * 1024 * 1024)
                    return (false, "Le logo ne doit pas dépasser 5 Mo", null);

                var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
                if (!allowed.Contains(ext))
                    return (false, "Format non supporté (jpg, png, webp, svg)", null);

                var (url, error) = await UploadImageAsync(logo, "tenant-logos");
                if (error != null)
                    return (false, $"Erreur upload logo : {error}", null);

                profile.LogoUrl = url;
            }

            // ── Mise à jour des champs ─────────────────────
            profile.JobTitle = dto.JobTitle;
            profile.Phone = dto.Phone;
            profile.Website = dto.Website;
            profile.Linkedin = dto.Linkedin;
            profile.Twitter = dto.Twitter;
            profile.HiringStatus = dto.HiringStatus ?? "actively";
            profile.WorkTypesJson = dto.WorkTypes != null
                ? JsonSerializer.Serialize(dto.WorkTypes) : "[]";
            profile.TechStackJson = dto.TechStack != null
                ? JsonSerializer.Serialize(dto.TechStack) : "[]";
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("✅ Profil tenant mis à jour: {id}", tenantId);
            return (true, "Profil enregistré avec succès", BuildDto(tenant, profile));
        }

        private static TenantProfileDto BuildDto(Tenant tenant, TenantProfile? profile)
        {
            return new TenantProfileDto
            {
                FullName = tenant.Utilisateur.Nom ?? "",
                Email = tenant.Utilisateur.Email ?? "",
                JobTitle = profile?.JobTitle,
                Phone = profile?.Phone,
                Website = profile?.Website,
                Linkedin = profile?.Linkedin,
                Twitter = profile?.Twitter,
                HiringStatus = profile?.HiringStatus ?? "actively",
                WorkTypes = string.IsNullOrEmpty(profile?.WorkTypesJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(profile.WorkTypesJson) ?? new(),
                TechStack = string.IsNullOrEmpty(profile?.TechStackJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(profile.TechStackJson) ?? new(),
                LogoUrl = profile?.LogoUrl,
            };
        }

        private async Task<(string? Url, string? Error)> UploadImageAsync(IFormFile file, string folder)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'upload Cloudinary");
                return (null, ex.Message);
            }
        }
    }
}