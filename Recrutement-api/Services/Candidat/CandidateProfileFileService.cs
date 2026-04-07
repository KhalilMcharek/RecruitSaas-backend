using Recrutement_api.DTOs.Candidate;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrutement_api.Services.CandidateServices
{
    public class CandidateProfileFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _dataFolder;
        private readonly string _imagesFolder;
        private readonly JsonSerializerOptions _jsonOptions;

        public CandidateProfileFileService(IWebHostEnvironment env)
        {
            _env = env;
            _dataFolder = Path.Combine(_env.WebRootPath, "data", "candidate-profiles");
            _imagesFolder = Path.Combine(_env.WebRootPath, "imagesCandidate");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Lit le profil candidat depuis le fichier JSON
        /// </summary>
        public async Task<CandidateProfileData?> GetProfileAsync(Guid candidateId)
        {
            var filePath = GetProfilePath(candidateId);
            if (!File.Exists(filePath)) return null;

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<CandidateProfileData>(json, _jsonOptions);
            
            // Garantir que les collections ne sont jamais null
            profile ??= new CandidateProfileData();
            profile.Skills ??= new List<string>();
            
            return profile;
        }

        /// <summary>
        /// Sauvegarde le profil candidat dans un fichier JSON + gère l'avatar
        /// </summary>
        public async Task<(bool Success, string? Message)> SaveProfileAsync(
            Guid candidateId, 
            UpdateCandidateProfileDto dto, 
            IFormFile? avatar,
            string? existingAvatarUrl)
        {
            try
            {
                // Créer le dossier de données si nécessaire
                Directory.CreateDirectory(_dataFolder);
                
                // ── Gestion de l'avatar ──────────────────────────────
                string? avatarUrl = existingAvatarUrl;
                
                if (avatar != null && avatar.Length > 0)
                {
                    // Validation : taille max 5MB
                    if (avatar.Length > 5 * 1024 * 1024)
                        return (false, "L'avatar ne doit pas dépasser 5 Mo");

                    // Validation : extensions autorisées
                    var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
                    var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
                    
                    if (!allowedExts.Contains(ext))
                        return (false, "Format d'image non supporté (jpg, png, webp, svg)");

                    // Créer le dossier images si nécessaire
                    Directory.CreateDirectory(_imagesFolder);
                    
                    // Supprimer l'ancien avatar local si présent
                    if (!string.IsNullOrEmpty(existingAvatarUrl) && 
                        existingAvatarUrl.Contains("/imagesCandidate/"))
                    {
                        try
                        {
                            var oldFileName = Path.GetFileName(new Uri(existingAvatarUrl).LocalPath);
                            var oldPath = Path.Combine(_imagesFolder, oldFileName);
                            if (File.Exists(oldPath)) File.Delete(oldPath);
                        }
                        catch { /* Ignorer les erreurs de suppression */ }
                    }
                    
                    // Sauvegarder le nouveau fichier
                    var fileName = $"{candidateId}_{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(_imagesFolder, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await avatar.CopyToAsync(stream);
                    
                    // URL relative pour le frontend (compatible UseStaticFiles)
                    avatarUrl = $"/imagesCandidate/{fileName}";
                }

                // ── Construction du profil ──────────────────────────
                var profile = new CandidateProfileData
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Location = dto.Location,
                    Bio = dto.Bio,
                    Seeking = dto.Seeking ?? "student",
                    Education = dto.Education,
                    FieldOfStudy = dto.FieldOfStudy,
                    Experience = dto.Experience,
                    Availability = dto.Availability,
                    Skills = dto.Skills ?? new List<string>(),
                    Linkedin = dto.Linkedin,
                    Github = dto.Github,
                    PortfolioUrl = dto.PortfolioUrl,
                    AvatarUrl = avatarUrl,
                    UpdatedAt = DateTime.UtcNow
                };

                // ── Sérialisation et sauvegarde ─────────────────────
                var json = JsonSerializer.Serialize(profile, _jsonOptions);
                await File.WriteAllTextAsync(GetProfilePath(candidateId), json);
                
                return (true, "Profil enregistré avec succès");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur lors de la sauvegarde : {ex.Message}");
            }
        }

        /// <summary>
        /// Supprime le profil candidat (JSON + avatar)
        /// </summary>
        public async Task<(bool Success, string? Message)> DeleteProfileAsync(Guid candidateId)
        {
            try
            {
                var jsonPath = GetProfilePath(candidateId);
                
                // Lire l'URL de l'avatar avant suppression
                string? avatarUrlToDelete = null;
                if (File.Exists(jsonPath))
                {
                    var raw = await File.ReadAllTextAsync(jsonPath);
                    var existing = JsonDocument.Parse(raw).RootElement;
                    
                    if (existing.TryGetProperty("avatarUrl", out var av) && av.ValueKind != JsonValueKind.Null)
                        avatarUrlToDelete = av.GetString();
                }

                // Supprimer le fichier JSON
                if (File.Exists(jsonPath))
                    File.Delete(jsonPath);

                // Supprimer l'avatar local si présent
                if (!string.IsNullOrEmpty(avatarUrlToDelete) && 
                    avatarUrlToDelete.Contains("/imagesCandidate/"))
                {
                    try
                    {
                        var fileName = Path.GetFileName(new Uri(avatarUrlToDelete).LocalPath);
                        var filePath = Path.Combine(_imagesFolder, fileName);
                        if (File.Exists(filePath))
                            File.Delete(filePath);
                    }
                    catch { /* Ignorer les erreurs de suppression */ }
                }

                return (true, "Profil supprimé avec succès");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur lors de la suppression : {ex.Message}");
            }
        }

        /// <summary>
        /// Retourne l'URL de l'avatar pour un candidat
        /// </summary>
        public string? GetAvatarUrl(Guid candidateId)
        {
            var profile = GetProfileAsync(candidateId).Result;
            return profile?.AvatarUrl;
        }

        /// <summary>
        /// Chemin complet vers le fichier JSON du profil
        /// </summary>
        private string GetProfilePath(Guid candidateId) => 
            Path.Combine(_dataFolder, $"{candidateId}.json");
    }

    // ═══════════════════════════════════════════════════════════════
    // Modèle interne pour la sérialisation/désérialisation JSON
    // ═══════════════════════════════════════════════════════════════
    public class CandidateProfileData
    {
        // Personal Information
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        
        // Career Status
        public string? Seeking { get; set; }
        public string? Education { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Experience { get; set; }
        public string? Availability { get; set; }
        
        // Skills & Links
        public List<string> Skills { get; set; } = new();
        public string? Linkedin { get; set; }
        public string? Github { get; set; }
        public string? PortfolioUrl { get; set; }
        
        // Metadata
        public DateTime UpdatedAt { get; set; }
    }
}