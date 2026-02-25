// Services/TenantService.cs
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Tenant;
using Recrutement_api.Models;

namespace Recrutement_api.Services
{
    public class TenantService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly string _logosPath;

        public TenantService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
            _logosPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "logos");
            
            if (!Directory.Exists(_logosPath))
                Directory.CreateDirectory(_logosPath);
        }

        // ============================================
        // MÉTHODES PUBLIQUES
        // ============================================

        // Récupérer toutes les entreprises d'un tenant
        public async Task<List<object>> GetCompaniesAsync(Guid tenantId)
        {
            return await _context.Entreprises
                .Where(e => e.TenantId == tenantId)
                .Select(e => new
                {
                    e.Id,
                    e.Nom,
                    e.Secteur,
                    e.RNE,
                    e.Description,
                    e.LogoUrl,
                    e.Status,
                    e.CreeLe,
                    ExpertsCount = _context.Experts.Count(exp => exp.EntrepriseId == e.Id),
                    OffresCount = _context.OffresEmploi.Count(o => o.EntrepriseId == e.Id)
                })
                .ToListAsync<object>();
        }

        // Récupérer une entreprise par son ID
        public async Task<object?> GetCompanyByIdAsync(Guid id, Guid tenantId)
        {
            return await _context.Entreprises
                .Where(e => e.Id == id && e.TenantId == tenantId)
                .Select(e => new
                {
                    e.Id,
                    e.Nom,
                    e.Secteur,
                    e.RNE,
                    e.Description,
                    e.LogoUrl,
                    e.Status,
                    e.CreeLe,
                    ExpertsCount = _context.Experts.Count(exp => exp.EntrepriseId == e.Id),
                    OffresCount = _context.OffresEmploi.Count(o => o.EntrepriseId == e.Id)
                })
                .FirstOrDefaultAsync();
        }

        // Créer une nouvelle entreprise
        public async Task<Entreprise> CreateCompanyAsync(Guid tenantId, CreateCompanyDto dto, IFormFile? logo)
        {
            var company = new Entreprise
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Nom = dto.Nom,
                Secteur = dto.Secteur,
                RNE = dto.RNE,
                Description = dto.Description,
                Status = "Pending",
                CreeLe = DateTime.UtcNow
            };

            // Upload du logo si fourni
            if (logo != null && logo.Length > 0)
            {
                company.LogoUrl = await SauvegarderLogoAsync(company.Id, logo);
            }

            _context.Entreprises.Add(company);
            await _context.SaveChangesAsync();

            return company;
        }

        // Mettre à jour une entreprise (infos + logo optionnel)
        public async Task<(bool success, string message, object? data)> UpdateCompanyAsync(Guid id, Guid tenantId, UpdateCompanyDto dto, IFormFile? logo)
        {
            var company = await GetCompanyEntityAsync(id, tenantId);
            if (company == null)
                return (false, "Entreprise non trouvée", null);

            // Mise à jour des champs texte
            if (!string.IsNullOrEmpty(dto.Nom))
                company.Nom = dto.Nom;

            if (!string.IsNullOrEmpty(dto.Secteur))
                company.Secteur = dto.Secteur;

            if (!string.IsNullOrEmpty(dto.RNE))
                company.RNE = dto.RNE;

            if (dto.Description != null)
                company.Description = dto.Description;

            // Mise à jour du logo si fourni
            if (logo != null && logo.Length > 0)
            {
                // Supprimer l'ancien logo
                if (!string.IsNullOrEmpty(company.LogoUrl))
                {
                    await SupprimerLogoAsync(company.LogoUrl);
                }

                // Upload nouveau logo
                company.LogoUrl = await SauvegarderLogoAsync(id, logo);
            }

            await _context.SaveChangesAsync();

            return (true, "Entreprise mise à jour avec succès", new 
            { 
                company.Id, 
                company.Nom, 
                company.LogoUrl,
                company.Secteur,
                company.RNE,
                company.Description
            });
        }

        // Supprimer une entreprise
        public async Task<(bool success, string message, object? data)> DeleteCompanyAsync(Guid id, Guid tenantId)
        {
            var company = await GetCompanyEntityAsync(id, tenantId);
            if (company == null)
                return (false, "Entreprise non trouvée", null);

            // Vérifier s'il y a des données liées
            var hasExperts = await _context.Experts.AnyAsync(e => e.EntrepriseId == id);
            var hasOffres = await _context.OffresEmploi.AnyAsync(o => o.EntrepriseId == id);

            if (hasExperts || hasOffres)
            {
                return (false, "Impossible de supprimer : l'entreprise a des experts ou des offres d'emploi", null);
            }

            // Supprimer le logo si existant
            if (!string.IsNullOrEmpty(company.LogoUrl))
            {
                await SupprimerLogoAsync(company.LogoUrl);
            }

            _context.Entreprises.Remove(company);
            await _context.SaveChangesAsync();

            return (true, "Entreprise supprimée avec succès", new { company.Id, company.Nom });
        }

        // Vérifier si l'entreprise appartient au tenant
        public async Task<bool> CompanyExistsAsync(Guid id, Guid tenantId)
        {
            return await _context.Entreprises
                .AnyAsync(e => e.Id == id && e.TenantId == tenantId);
        }

        // ============================================
        // MÉTHODES PRIVÉES
        // ============================================

        // Récupérer l'entité complète pour modification
        private async Task<Entreprise?> GetCompanyEntityAsync(Guid id, Guid tenantId)
        {
            return await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
        }

        // Sauvegarder un logo sur le disque
        private async Task<string> SauvegarderLogoAsync(Guid entrepriseId, IFormFile logo)
        {
            var fileName = $"logo_{entrepriseId}_{DateTime.Now.Ticks}{Path.GetExtension(logo.FileName)}";
            var filePath = Path.Combine(_logosPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await logo.CopyToAsync(stream);

            return $"/uploads/logos/{fileName}";
        }

        // Supprimer un logo du disque
        private async Task SupprimerLogoAsync(string logoUrl)
        {
            var fileName = Path.GetFileName(logoUrl);
            var filePath = Path.Combine(_logosPath, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            await Task.CompletedTask;
        }
    }
}