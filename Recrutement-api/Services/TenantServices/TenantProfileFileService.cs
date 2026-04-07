using Recrutement_api.DTOs.Tenant;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrutement_api.Services.TenantServices
{
    public class TenantProfileFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _dataFolder;
        private readonly string _imagesFolder;
        private readonly JsonSerializerOptions _jsonOptions;

        public TenantProfileFileService(IWebHostEnvironment env)
        {
            _env = env;
            _dataFolder = Path.Combine(_env.WebRootPath, "data", "tenant-profiles");
            _imagesFolder = Path.Combine(_env.WebRootPath, "imagesProfile");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<TenantProfileData?> GetProfileAsync(Guid tenantId)
        {
            var filePath = GetProfilePath(tenantId);
            if (!File.Exists(filePath)) return null;

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<TenantProfileData>(json, _jsonOptions);
            
            // Ensure collections are never null
            profile ??= new TenantProfileData();
            profile.WorkTypes ??= new List<string>();
            profile.TechStack ??= new List<string>();
            
            return profile;
        }

        public async Task<bool> SaveProfileAsync(Guid tenantId, UpdateTenantProfileDto dto, 
            IFormFile? logo, string? existingLogoUrl, int existingActiveJobs, int existingTotalHires)
        {
            Directory.CreateDirectory(_dataFolder);
            
            // ── Gestion du logo ──
            string? logoUrl = existingLogoUrl;
            
            if (logo != null && logo.Length > 0)
            {
                Directory.CreateDirectory(_imagesFolder);
                
                var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
                
                if (!allowedExts.Contains(ext))
                    return false; // Extension non autorisée
                
                // Supprimer l'ancien logo si c'était un fichier local
                if (!string.IsNullOrEmpty(existingLogoUrl) && 
                    existingLogoUrl.Contains("/imagesProfile/"))
                {
                    try
                    {
                        var oldFileName = Path.GetFileName(new Uri(existingLogoUrl).LocalPath);
                        var oldPath = Path.Combine(_imagesFolder, oldFileName);
                        if (File.Exists(oldPath)) File.Delete(oldPath);
                    }
                    catch { /* Ignorer les erreurs de suppression */ }
                }
                
                var fileName = $"{tenantId}_{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(_imagesFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await logo.CopyToAsync(stream);
                
                logoUrl = $"/imagesProfile/{fileName}";
            }

            // ── Construction du profil ──
            var profile = new TenantProfileData
            {
                CompanyName = dto.CompanyName,
                Industry = dto.Industry,
                CompanySize = dto.CompanySize,
                Location = dto.Location,
                Description = dto.Description,
                Website = dto.Website,
                Linkedin = dto.Linkedin,
                Twitter = dto.Twitter,
                FullName = dto.FullName,
                Email = dto.Email,
                JobTitle = dto.JobTitle,
                Phone = dto.Phone,
                HiringStatus = dto.HiringStatus ?? "actively",
                WorkTypes = dto.WorkTypes ?? new List<string>(),
                TechStack = dto.TechStack ?? new List<string>(),
                LogoUrl = logoUrl,
                ActiveJobs = existingActiveJobs,
                TotalHires = existingTotalHires,
                UpdatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            await File.WriteAllTextAsync(GetProfilePath(tenantId), json);
            
            return true;
        }

        public string? GetLogoUrl(Guid tenantId)
        {
            var profile = GetProfileAsync(tenantId).Result;
            return profile?.LogoUrl;
        }

        private string GetProfilePath(Guid tenantId) => 
            Path.Combine(_dataFolder, $"{tenantId}.json");
    }

    // Modèle interne pour la sérialisation
    public class TenantProfileData
    {
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }
        public string? HiringStatus { get; set; }
        public List<string> WorkTypes { get; set; } = new();
        public List<string> TechStack { get; set; } = new();
        public string? LogoUrl { get; set; }
        public int ActiveJobs { get; set; }
        public int TotalHires { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}