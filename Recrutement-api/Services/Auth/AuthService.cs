using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;

namespace Recrutement_api.Services
{
    // =========================================================
    // INTERFACE
    // =========================================================
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);

        Task<AuthResponseDto> RegisterCandidateAsync(RegisterDto dto);
        Task<string> RegisterCompanyAsync(CompanyRegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);

        Task<List<Tenant>> GetPendingTenantsAsync();
        Task ApproveTenantAsync(Guid tenantId);
        Task RejectTenantAsync(Guid tenantId);
    }

    // =========================================================
    // SERVICE IMPLEMENTATION
    // =========================================================
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<string>();
        }

        // ============================
        // PASSWORD MANAGEMENT
        // ============================
        public string HashPassword(string password)
        {
            return _passwordHasher.HashPassword(null, password);
        }

        public bool VerifyPassword(string password, string hash)
        {
            var result = _passwordHasher.VerifyHashedPassword(null, hash, password);
            return result == PasswordVerificationResult.Success;
        }

        // ============================
        // REGISTER CANDIDATE
        // ============================
        public async Task<AuthResponseDto> RegisterCandidateAsync(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.MotDePasse))
                throw new Exception("Email and password are required.");

            var emailExists = await _context.Utilisateurs
                .AnyAsync(u => u.Email == dto.Email);

            if (emailExists)
                throw new Exception("A user with this email already exists.");

            var user = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                Nom = dto.Nom ?? "Nom non précisé",
                Prenom = dto.Prenom ?? "Prénom non précisé",
                MotDePasseHash = HashPassword(dto.MotDePasse),
                Role = RoleUtilisateur.Candidat,
                CreeLe = DateTime.UtcNow
            };

            await _context.Utilisateurs.AddAsync(user);
            await _context.SaveChangesAsync();

            var candidat = new Candidat
            {
                Id = Guid.NewGuid(),
                UtilisateurId = user.Id,
                Telephone = "Non précisé",
                CreeLe = DateTime.UtcNow
            };

            await _context.Candidats.AddAsync(candidat);
            await _context.SaveChangesAsync();

            return GenerateAuthResponse(user);
        }

        // ============================
        // REGISTER COMPANY (TENANT)
        // ============================
        public async Task<string> RegisterCompanyAsync(CompanyRegisterDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.WorkEmail) ||
        string.IsNullOrWhiteSpace(dto.Password) ||
        string.IsNullOrWhiteSpace(dto.tenantName))
        throw new Exception("Required fields are missing.");

    var emailExists = await _context.Utilisateurs
        .AnyAsync(u => u.Email == dto.WorkEmail);

    if (emailExists)
        throw new Exception("A user with this email already exists.");

    // 1️⃣ Création du CEO
    var ceo = new Utilisateur
    {
        Id = Guid.NewGuid(),
        Email = dto.WorkEmail,
        Nom = dto.tenantName,
        Prenom = dto.Prenom ?? "",
        Role = RoleUtilisateur.Tenant,
        MotDePasseHash = HashPassword(dto.Password),
        CreeLe = DateTime.UtcNow
    };
    await _context.Utilisateurs.AddAsync(ceo);
    await _context.SaveChangesAsync(); // ✅ Sauvegarde le CEO d'abord

    // 2️⃣ Insertion du Tenant via SQL brut pour inclure Nom dès l'INSERT
    var tenantId = Guid.NewGuid();
    await _context.Database.ExecuteSqlRawAsync(
        @"INSERT INTO ""Tenants"" (""Id"", ""CreeLe"", ""Nom"", ""Statut"", ""UtilisateurId"")
          VALUES ({0}, {1}, {2}, {3}, {4})",
        tenantId,
        DateTime.UtcNow,
        ceo.Nom,           // ✅ Nom = même valeur que l'utilisateur
        (int)TenantStatus.Pending,
        ceo.Id
    );

    // 3️⃣ Création de l'entreprise
    var entreprise = new Entreprise
    {
        Id = Guid.NewGuid(),
        Nom = dto.CompanyName ?? "Entreprise",
        Description = dto.Description ?? "Description non fournie",
        RNE = dto.RNE ?? "Non précisé",
        Secteur = dto.Secteur ?? "",
        LogoUrl = dto.LogoUrl ?? "",
        TenantId = tenantId,
        CreeLe = DateTime.UtcNow
    };
    await _context.Entreprises.AddAsync(entreprise);
    await _context.SaveChangesAsync(); // ✅ Sauvegarde l'entreprise

    return "Registration request successfully submitted. Awaiting admin approval.";
}
        // ============================
        // LOGIN
        // ============================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.MotDePasse))
                throw new Exception("Email and password required.");

            var user = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null ||
                !VerifyPassword(dto.MotDePasse, user.MotDePasseHash))
                throw new Exception("Incorrect email or password.");

            if (user.Role == RoleUtilisateur.Tenant)
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.UtilisateurId == user.Id);

                if (tenant == null || tenant.Statut != TenantStatus.Approved)
                    throw new Exception("Your company request has not been approved yet.");
            }

            return GenerateAuthResponse(user);
        }

        // ============================
        // ADMIN METHODS
        // ============================
        public async Task<List<Tenant>> GetPendingTenantsAsync()
        {
            return await _context.Tenants
                .Where(t => t.Statut == TenantStatus.Pending)
                .Include(t => t.Entreprises)
                .ToListAsync();
        }

        public async Task ApproveTenantAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new Exception("Tenant not found.");

            tenant.Statut = TenantStatus.Approved;
            await _context.SaveChangesAsync();
        }

        public async Task RejectTenantAsync(Guid tenantId)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new Exception("Tenant not found.");

            tenant.Statut = TenantStatus.Rejected;
            await _context.SaveChangesAsync();
        }

        // ============================
        // AUTH RESPONSE
        // ============================
        private AuthResponseDto GenerateAuthResponse(Utilisateur user)
        {
            return new AuthResponseDto
            {
                Token = "FAKE_JWT_FOR_NOW",
                Email = user.Email,
                Role = user.Role
            };
        }
    }
}