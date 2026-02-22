using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;
using Microsoft.AspNetCore.Identity;

namespace Recrutement_api.Services
{
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
        Task<Tenant> RegisterTenantAsync(CompanyRegisterDto dto);
        Task<List<Tenant>> GetPendingTenantsAsync();
        Task ApproveTenantAsync(Guid tenantId);
        Task RejectTenantAsync(Guid tenantId);
        AuthResponseDto GenerateAuthResponse(Utilisateur user);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<string>();
        }

        public string HashPassword(string password)
        {
            return _passwordHasher.HashPassword(null, password);
        }

        public bool VerifyPassword(string password, string hash)
        {
            var result = _passwordHasher.VerifyHashedPassword(null, hash, password);
            return result == PasswordVerificationResult.Success;
        }

        // =============================================
        // REGISTER TENANT + ENTREPRISE
        // =============================================
        public async Task<Tenant> RegisterTenantAsync(CompanyRegisterDto dto)
        {
            // Vérifier si le tenant existe déjà
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Nom == dto.tenantName);
            if (existingTenant != null)
                throw new InvalidOperationException("Un tenant avec ce nom existe déjà.");

            // Créer tenant + entreprise
            var tenant = new Tenant
            {
                Nom = dto.tenantName,
                Statut = TenantStatus.Pending,
                CreeLe = DateTime.UtcNow,
                Entreprises = new List<Entreprise>
                {
                    new Entreprise
                    {
                        Nom = dto.CompanyName,
                        RNE = dto.RNE,
                        Secteur = dto.Secteur
                    }
                }
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            return tenant;
        }

        // =============================================
        // ADMIN : Liste tenants en attente
        // =============================================
        public async Task<List<Tenant>> GetPendingTenantsAsync()
        {
            return await _context.Tenants
                .Where(t => t.Statut == TenantStatus.Pending)
                .Include(t => t.Entreprises)
                .ToListAsync();
        }

        // =============================================
        // ADMIN : Approuver tenant
        // =============================================
        public async Task ApproveTenantAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("Tenant non trouvé.");

            tenant.Statut = TenantStatus.Approved;
            await _context.SaveChangesAsync();
        }

        // =============================================
        // ADMIN : Rejeter tenant
        // =============================================
        public async Task RejectTenantAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("Tenant non trouvé.");

            tenant.Statut = TenantStatus.Rejected;
            await _context.SaveChangesAsync();
        }

        // =============================================
        // GENERER AUTH RESPONSE
        // =============================================
        public AuthResponseDto GenerateAuthResponse(Utilisateur user)
        {
            // Ici tu peux ajouter le JWT comme avant
            return new AuthResponseDto
            {
                Token = "FAKE_JWT_POUR_EXEMPLE",
                Email = user.Email,
                Role = user.Role
            };
        }
    }
}