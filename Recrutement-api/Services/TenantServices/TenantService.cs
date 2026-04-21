// Services/TenantService.cs
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Tenant;
using Recrutement_api.Models;

namespace Recrutement_api.Services.TenantServices
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

        // ============================================================
        // RÉCUPÉRER LES ENTREPRISES — vérifie que le tenant est Approved
        // ============================================================
        public async Task<(bool success, string message, List<object>? data, string? statut)> GetCompaniesAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                return (false, "Tenant introuvable", null, null);

            if (tenant.Statut != TenantStatus.Approved)
                return (false, $"Compte non approuvé (statut : {tenant.Statut})", null, tenant.Statut.ToString());

            var companies = await _context.Entreprises
                .Where(e => e.TenantId == tenantId)
                .Select(e => new
                {
                    e.Id,
                    e.Nom,
                    e.Secteur,
                    e.RNE,
                    e.Description,
                    e.LogoUrl,
                    e.CreeLe,
                    ExpertsCount = _context.Experts.Count(exp => exp.EntrepriseId == e.Id),
                    OffresCount = _context.OffresEmploi.Count(o => o.EntrepriseId == e.Id)
                })
                .ToListAsync<object>();

            return (true, "OK", companies, tenant.Statut.ToString());
        }

        // ============================================================
        // RÉCUPÉRER UNE ENTREPRISE PAR ID
        // ============================================================
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
                    e.CreeLe,
                    ExpertsCount = _context.Experts.Count(exp => exp.EntrepriseId == e.Id),
                    OffresCount = _context.OffresEmploi.Count(o => o.EntrepriseId == e.Id)
                })
                .FirstOrDefaultAsync();
        }

        // ============================================================
        // CRÉER UNE ENTREPRISE
        // ============================================================
        public async Task<(bool success, string message, Entreprise? data)> CreateCompanyAsync(Guid tenantId, CreateCompanyDto dto, IFormFile? logo)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Entreprises)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                return (false, "Tenant introuvable", null);

            var isFirstCompany = !tenant.Entreprises.Any();

            // Pas la première entreprise → doit être Approved
            if (!isFirstCompany && tenant.Statut != TenantStatus.Approved)
                return (false, $"Votre compte doit être approuvé (statut : {tenant.Statut})", null);

            // Première entreprise → passe en Pending
            if (isFirstCompany && tenant.Statut != TenantStatus.Pending)
                tenant.Statut = TenantStatus.Pending;

            var company = new Entreprise
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Nom = dto.Nom,
                Secteur = dto.Secteur,
                RNE = dto.RNE,
                Description = dto.Description,
                CreeLe = DateTime.UtcNow
            };

            if (logo != null && logo.Length > 0)
                company.LogoUrl = await SauvegarderLogoAsync(company.Id, logo);

            _context.Entreprises.Add(company);
            await _context.SaveChangesAsync();

            return (true, isFirstCompany
                ? "Demande soumise, en attente d'approbation"
                : "Entreprise créée avec succès", company);
        }

        // ============================================================
        // MODIFIER UNE ENTREPRISE
        // ============================================================
        public async Task<(bool success, string message, object? data)> UpdateCompanyAsync(Guid id, Guid tenantId, UpdateCompanyDto dto, IFormFile? logo)
        {
            var company = await GetCompanyEntityAsync(id, tenantId);
            if (company == null)
                return (false, "Entreprise non trouvée", null);

            if (!string.IsNullOrEmpty(dto.Nom)) company.Nom = dto.Nom;
            if (!string.IsNullOrEmpty(dto.Secteur)) company.Secteur = dto.Secteur;
            if (!string.IsNullOrEmpty(dto.RNE)) company.RNE = dto.RNE;
            if (dto.Description != null) company.Description = dto.Description;

            if (logo != null && logo.Length > 0)
            {
                if (!string.IsNullOrEmpty(company.LogoUrl))
                    await SupprimerLogoAsync(company.LogoUrl);
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

        // ============================================================
        // SUPPRIMER UNE ENTREPRISE
        // ============================================================
        public async Task<(bool success, string message, object? data)> DeleteCompanyAsync(Guid id, Guid tenantId)
        {
            var company = await GetCompanyEntityAsync(id, tenantId);
            if (company == null)
                return (false, "Entreprise non trouvée", null);

            var hasExperts = await _context.Experts.AnyAsync(e => e.EntrepriseId == id);
            var hasOffres = await _context.OffresEmploi.AnyAsync(o => o.EntrepriseId == id);

            if (hasExperts || hasOffres)
                return (false, "Impossible de supprimer : l'entreprise a des experts ou des offres", null);

            if (!string.IsNullOrEmpty(company.LogoUrl))
                await SupprimerLogoAsync(company.LogoUrl);

            _context.Entreprises.Remove(company);
            await _context.SaveChangesAsync();

            return (true, "Entreprise supprimée avec succès", new { company.Id, company.Nom });
        }



        // ============================================================
        // MÉTHODES PRIVÉES
        // ============================================================
        private async Task<Entreprise?> GetCompanyEntityAsync(Guid id, Guid tenantId)
        {
            return await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
        }

        private async Task<string> SauvegarderLogoAsync(Guid entrepriseId, IFormFile logo)
        {
            var extension = logo.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => Path.GetExtension(logo.FileName)
            };

            var fileName = $"logo_{entrepriseId}_{DateTime.Now.Ticks}{extension}";
            var filePath = Path.Combine(_logosPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await logo.CopyToAsync(stream);

            return $"/uploads/logos/{fileName}";
        }

        private async Task SupprimerLogoAsync(string logoUrl)
        {
            var fileName = Path.GetFileName(logoUrl);
            var filePath = Path.Combine(_logosPath, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
            await Task.CompletedTask;
        }
    }
}