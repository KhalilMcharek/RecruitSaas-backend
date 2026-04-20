// Services/AdminService.cs
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.TenantRequest;
using Recrutement_api.DTOs.Admin;
using Recrutement_api.Models;
using System.Text.Json;
using System.IO;

namespace Recrutement_api.Services
{
    public class AdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAuthService _authService;

        public AdminService(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IAuthService authService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _authService = authService;
        }

        // ============================================================
        // LISTE DE TOUTES LES DEMANDES
        // ============================================================
        public async Task<List<TenantRequestListDto>> GetAllTenantRequestsAsync()
        {
            return await _context.Tenants
                .Include(t => t.Utilisateur)
                .Include(t => t.Entreprises)
                .OrderByDescending(t => t.CreeLe)
                .Select(t => new TenantRequestListDto
                {
                    CompanyName = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.Nom).FirstOrDefault() ?? string.Empty,
                    RNE = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.RNE).FirstOrDefault() ?? string.Empty,
                    Owner = t.Utilisateur != null ? t.Utilisateur.Nom : null,
                    Industry = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.Secteur).FirstOrDefault() ?? string.Empty,
                    Status = t.Statut.ToString(),
                    LogoUrl = t.Entreprises.OrderBy(e => e.CreeLe).Select(e => e.LogoUrl).FirstOrDefault()
                })
                .ToListAsync();
        }

        // ============================================================
        // DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // ============================================================
        public async Task<TenantRequestDetailDto?> GetTenantRequestByNameAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                    .ThenInclude(t => t.Utilisateur)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return null;

            return new TenantRequestDetailDto
            {
                Id = entreprise.Id,
                CompanyName = entreprise.Nom,
                RNE = entreprise.RNE ?? string.Empty,
                Owner = entreprise.Tenant?.Utilisateur?.Nom,
                WorkEmail = entreprise.Tenant?.Utilisateur?.Email,
                Industry = entreprise.Secteur ?? string.Empty,
                Description = entreprise.Description,
                LogoUrl = entreprise.LogoUrl,
                Status = entreprise.Tenant?.Statut.ToString(),
                CreatedAt = entreprise.CreeLe
            };
        }

        // ============================================================
        // APPROUVER → Tenant devient Approved
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> ApproveTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Tenant == null)
                return (false, "Tenant associé introuvable", null);

            if (entreprise.Tenant.Statut != TenantStatus.Pending)
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = TenantStatus.Approved;
            await _context.SaveChangesAsync();

            return (true, $"'{entreprise.Nom}' a été approuvée", new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut.ToString()
            });
        }

        // ============================================================
        // REJETER → Tenant devient Rejected
        // ============================================================
        public async Task<(bool success, string message, TenantStatusResponseDto? data)> RejectTenantRequestAsync(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                .FirstOrDefaultAsync(e => e.Nom.ToLower() == decoded.ToLower());

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée pour '{decoded}'", null);

            if (entreprise.Tenant == null)
                return (false, "Tenant associé introuvable", null);

            if (entreprise.Tenant.Statut != TenantStatus.Pending)
                return (false, $"Cette demande a déjà été traitée (statut : {entreprise.Tenant.Statut})", null);

            entreprise.Tenant.Statut = TenantStatus.Rejected;
            await _context.SaveChangesAsync();

            return (true, $"'{entreprise.Nom}' a été rejetée", new TenantStatusResponseDto
            {
                CompanyName = entreprise.Nom,
                Status = entreprise.Tenant.Statut.ToString()
            });
        }

        // ============================================================
        // STATISTIQUES DASHBOARD
        // ============================================================
        public async Task<object> GetDashboardStatsAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var tenants = await _context.Tenants.ToListAsync();

            var totalJobs = await _context.OffresEmploi.CountAsync();

            var newTenantsToday = await _context.Tenants
                .CountAsync(t => t.CreeLe.Date == today);

            var pendingCount = tenants.Count(t => t.Statut == TenantStatus.Pending);
            var approvedCount = tenants.Count(t => t.Statut == TenantStatus.Approved);
            var rejectedCount = tenants.Count(t => t.Statut == TenantStatus.Rejected);

            var processedCount = approvedCount + rejectedCount;
            var processingRatePercent = tenants.Count == 0
                ? 0
                : (int)Math.Round((processedCount * 100.0) / tenants.Count);

            // Activité des 5 derniers jours (incluant aujourd'hui) basée sur la création des tenants (demandes)
            var days = Enumerable.Range(0, 5)
                .Select(i => today.AddDays(-(4 - i))) // 5 dates, du plus ancien au plus récent
                .ToArray();

            var createdLast5Days = await _context.Tenants
                .Where(t => t.CreeLe.Date >= days.First() && t.CreeLe.Date <= days.Last())
                .GroupBy(t => t.CreeLe.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var tenantsCreatedLast5Days = days
                .Select(d => createdLast5Days.FirstOrDefault(x => x.Day == d)?.Count ?? 0)
                .ToArray();

            return new
            {
                total = tenants.Count,
                pending = pendingCount,
                approved = approvedCount,
                rejected = rejectedCount,
                totalJobs,
                newTenantsToday,
                processed = processedCount,
                processingRatePercent,
                tenantsCreatedLast5Days
            };
        }

        // ============================================================
        // LISTE DES TENANTS DEPUIS LES FICHIERS JSON
        // ============================================================
        public async Task<List<TenantProfileFromFileDto>> GetTenantsFromJsonFilesAsync()
        {
            var tenants = new List<TenantProfileFromFileDto>();

            var profilesPath = Path.Combine(_webHostEnvironment.WebRootPath, "data", "tenant-profiles");

            if (!Directory.Exists(profilesPath))
                throw new DirectoryNotFoundException($"Le dossier des profils tenants n'existe pas: {profilesPath}");

            var jsonFiles = Directory.GetFiles(profilesPath, "*.json");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(filePath);
                    var tenant = JsonSerializer.Deserialize<TenantProfileFromFileDto>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tenant != null && tenant.Status?.ToLower() == "approved")
                    {
                        if (!string.IsNullOrEmpty(tenant.LogoUrl))
                        {
                            if (!tenant.LogoUrl.StartsWith("/") && !tenant.LogoUrl.StartsWith("http"))
                                tenant.LogoUrl = $"/imagesProfiles/{tenant.LogoUrl}";
                        }

                        tenants.Add(tenant);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de la lecture du fichier {filePath}: {ex.Message}");
                }
            }

            return tenants.OrderByDescending(t => t.ApprovedAt).ToList();
        }

        // ============================================================
        // LISTE DES TENANTS (DEPUIS LA BASE DE DONNÉES)
        // ============================================================
        public async Task<List<TenantListDto>> GetAllTenantsAsync(string? status = null)
        {
            var query = _context.Tenants
                .Include(t => t.Utilisateur)
                .Include(t => t.Entreprises)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TenantStatus>(status, true, out var tenantStatus))
                query = query.Where(t => t.Statut == tenantStatus);
            else
                query = query.Where(t => t.Statut == TenantStatus.Approved);

            var tenantEntities = await query
                .OrderByDescending(t => t.CreeLe)
                .ToListAsync();

            if (tenantEntities.Count == 0)
                return new List<TenantListDto>();

            var tenantIds = tenantEntities.Select(t => t.Id).ToList();
            var profiles = await _context.TenantProfiles
                .Where(p => tenantIds.Contains(p.TenantId))
                .ToDictionaryAsync(p => p.TenantId);

            var rneList = tenantEntities
                .Select(t => t.Entreprises?.OrderBy(e => e.CreeLe).FirstOrDefault()?.RNE)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            var jobCountsByRne = await _context.OffresEmploi
                .AsNoTracking()
                .Where(o => o.Entreprise != null && o.Entreprise.RNE != null && rneList.Contains(o.Entreprise.RNE))
                .GroupBy(o => o.Entreprise!.RNE!)
                .Select(g => new { Rne = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Rne, x => x.Count);

            var list = new List<TenantListDto>(tenantEntities.Count);
            foreach (var t in tenantEntities)
            {
                var firstCo = t.Entreprises?.OrderBy(e => e.CreeLe).FirstOrDefault();
                var rne = firstCo?.RNE ?? string.Empty;
                profiles.TryGetValue(t.Id, out var prof);

                var companyLogo = firstCo?.LogoUrl;
                var profileLogo = prof?.LogoUrl;
                var logo = !string.IsNullOrWhiteSpace(profileLogo) ? profileLogo : companyLogo;

                jobCountsByRne.TryGetValue(rne, out var jobs);

                list.Add(new TenantListDto
                {
                    TenantId = t.Id,
                    TenantDisplayName = BuildTenantDisplayName(t.Utilisateur),
                    CompanyName = firstCo?.Nom ?? string.Empty,
                    RNE = rne,
                    Owner = t.Utilisateur?.Nom,
                    WorkEmail = t.Utilisateur?.Email,
                    Phone = prof?.Phone,
                    Industry = firstCo?.Secteur ?? string.Empty,
                    LogoUrl = logo,
                    ApprovedAt = t.CreeLe,
                    ActiveJobs = jobs,
                    Status = t.Statut.ToString(),
                    JobTitle = prof?.JobTitle,
                    Website = prof?.Website,
                    Linkedin = prof?.Linkedin,
                    Twitter = prof?.Twitter,
                    HiringStatus = prof?.HiringStatus,
                    WorkTypesJson = prof?.WorkTypesJson,
                    TechStackJson = prof?.TechStackJson,
                    ProfileUpdatedAt = prof?.UpdatedAt
                });
            }

            return list;
        }

        // ============================================================
        // SUSPENDRE UN TENANT
        // ============================================================
        public async Task<(bool success, string message)> SuspendTenantAsync(string rne)
        {
            var decoded = Uri.UnescapeDataString(rne);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                .FirstOrDefaultAsync(e => e.RNE != null && e.RNE == decoded);

            if (entreprise == null)
                return (false, $"Aucune entreprise trouvée avec le RNE '{decoded}'");

            if (entreprise.Tenant == null)
                return (false, "Tenant associé introuvable");

            if (entreprise.Tenant.Statut != TenantStatus.Approved)
                return (false, $"Seuls les tenants approuvés peuvent être suspendus (statut actuel : {entreprise.Tenant.Statut})");

            entreprise.Tenant.Statut = TenantStatus.Rejected;
            await _context.SaveChangesAsync();

            return (true, $"Le tenant '{entreprise.Nom}' a été suspendu avec succès");
        }

        // ============================================================
        // DÉTAIL D'UN TENANT PAR RNE
        // ============================================================
        public async Task<TenantDetailDto?> GetTenantByRneAsync(string rne)
        {
            var decoded = Uri.UnescapeDataString(rne);

            var entreprise = await _context.Entreprises
                .Include(e => e.Tenant)
                    .ThenInclude(t => t.Utilisateur)
                .FirstOrDefaultAsync(e => e.RNE != null && e.RNE == decoded);

            if (entreprise == null)
                return null;

            var activeJobs = await _context.OffresEmploi
                .CountAsync(o => o.Entreprise != null && o.Entreprise.RNE == decoded);

            var tenantId = entreprise.Tenant?.Id;
            TenantProfile? prof = null;
            if (tenantId != null)
                prof = await _context.TenantProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId.Value);

            var companyLogo = entreprise.LogoUrl;
            var profileLogo = prof?.LogoUrl;
            var logo = !string.IsNullOrWhiteSpace(profileLogo) ? profileLogo : companyLogo;

            return new TenantDetailDto
            {
                TenantId = tenantId ?? Guid.Empty,
                TenantDisplayName = BuildTenantDisplayName(entreprise.Tenant?.Utilisateur),
                CompanyName = entreprise.Nom,
                RNE = entreprise.RNE ?? string.Empty,
                Owner = entreprise.Tenant?.Utilisateur?.Nom,
                WorkEmail = entreprise.Tenant?.Utilisateur?.Email,
                Phone = prof?.Phone,
                Industry = entreprise.Secteur ?? string.Empty,
                Description = entreprise.Description,
                LogoUrl = logo,
                ApprovedAt = entreprise.Tenant?.CreeLe,
                ActiveJobs = activeJobs,
                Status = entreprise.Tenant?.Statut.ToString(),
                Address = null,
                Website = prof?.Website,
                EmployeeCount = null,
                JobTitle = prof?.JobTitle,
                Linkedin = prof?.Linkedin,
                Twitter = prof?.Twitter,
                HiringStatus = prof?.HiringStatus,
                WorkTypesJson = prof?.WorkTypesJson,
                TechStackJson = prof?.TechStackJson,
                ProfileUpdatedAt = prof?.UpdatedAt
            };
        }

        // ============================================================
        // PROFIL ADMIN
        // ============================================================
        public async Task<AdminProfileDto?> GetAdminProfileAsync(Guid userId)
        {
            var utilisateur = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Id == userId && u.Role == RoleUtilisateur.Admin);

            if (utilisateur == null)
                return null;

            return new AdminProfileDto
            {
                Id = utilisateur.Id,
                FirstName = utilisateur.Prenom ?? string.Empty,
                LastName = utilisateur.Nom ?? string.Empty,
                Email = utilisateur.Email ?? string.Empty,
                Phone = null,
                Role = utilisateur.Role.ToString(),
                CreatedAt = utilisateur.CreeLe
            };
        }

        public async Task<(bool success, string message)> UpdateAdminProfileAsync(Guid userId, UpdateAdminProfileDto updateDto)
        {
            var utilisateur = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Id == userId && u.Role == RoleUtilisateur.Admin);

            if (utilisateur == null)
                return (false, "Admin non trouvé");

            utilisateur.Prenom = updateDto.FirstName;
            utilisateur.Nom = updateDto.LastName;
            utilisateur.Email = updateDto.Email;

            await _context.SaveChangesAsync();

            return (true, "Profil mis à jour avec succès");
        }

        // ============================================================
        // CHANGER LE MOT DE PASSE — même logique que AuthService (Identity + BCrypt, sans exception Base64)
        // ============================================================
        public async Task<(bool success, string message)> ChangeAdminPasswordAsync(Guid userId, AdminChangePasswordDto changePasswordDto)
        {
            var utilisateur = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Id == userId && u.Role == RoleUtilisateur.Admin);

            if (utilisateur == null)
                return (false, "Admin non trouvé");

            if (!_authService.VerifyPassword(changePasswordDto.CurrentPassword, utilisateur.MotDePasseHash ?? string.Empty))
                return (false, "Mot de passe actuel incorrect");

            utilisateur.MotDePasseHash = _authService.HashPassword(changePasswordDto.NewPassword);
            await _context.SaveChangesAsync();

            return (true, "Mot de passe mis à jour avec succès");
        }

        private static string? BuildTenantDisplayName(Utilisateur? u)
        {
            if (u == null) return null;
            var prenom = string.IsNullOrWhiteSpace(u.Prenom) ? null : u.Prenom.Trim();
            var nom = string.IsNullOrWhiteSpace(u.Nom) ? null : u.Nom.Trim();
            if (!string.IsNullOrEmpty(prenom) && !string.IsNullOrEmpty(nom))
                return $"{prenom} {nom}";
            if (!string.IsNullOrEmpty(nom)) return nom;
            if (!string.IsNullOrEmpty(prenom)) return prenom;
            return string.IsNullOrWhiteSpace(u.Email) ? null : u.Email.Trim();
        }
    }
}