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

        Task<string> RegisterCandidateAsync(RegisterDto dto);
        Task<string> RegisterCompanyAsync(CompanyRegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
    }

    // =========================================================
    // SERVICE IMPLEMENTATION
    // =========================================================
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly PasswordHasher<string> _passwordHasher;

        public AuthService(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
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
        public async Task<string> RegisterCandidateAsync(RegisterDto dto)
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
                Nom = dto.Nom,
                Prenom = null,
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
                Telephone = null,
                CreeLe = DateTime.UtcNow
            };

            await _context.Candidats.AddAsync(candidat);
            await _context.SaveChangesAsync();

            return "Your account has been created successfully. Welcome aboard!";
        }

        // ============================
        // REGISTER COMPANY (TENANT)
        // ============================
        public async Task<string> RegisterCompanyAsync(CompanyRegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.WorkEmail) ||
                string.IsNullOrWhiteSpace(dto.Password) ||
                string.IsNullOrWhiteSpace(dto.TenantName))
                throw new Exception("Required fields are missing.");

            var emailExists = await _context.Utilisateurs
                .AnyAsync(u => u.Email == dto.WorkEmail);

            if (emailExists)
                throw new Exception("A user with this email already exists.");

            var user = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Email = dto.WorkEmail,
                Nom = dto.TenantName,
                Prenom = null,
                Role = RoleUtilisateur.Tenant,
                MotDePasseHash = HashPassword(dto.Password),
                CreeLe = DateTime.UtcNow
            };
            await _context.Utilisateurs.AddAsync(user);
            await _context.SaveChangesAsync();

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                UtilisateurId = user.Id,
                Statut = TenantStatus.Pending,
                CreeLe = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(tenant);
            await _context.SaveChangesAsync();

            var entreprise = new Entreprise
            {
                Id = Guid.NewGuid(),
                Nom = dto.CompanyName,
                RNE = dto.RNE,
                Secteur = dto.Industry,
                Description = string.Empty,
                LogoUrl = string.Empty,
                TenantId = tenant.Id,
                CreeLe = DateTime.UtcNow
            };
            await _context.Entreprises.AddAsync(entreprise);
            await _context.SaveChangesAsync();

            return "Your company account has been registered successfully. It is currently under review — an administrator will confirm it shortly.";
        }

        // ============================
        // LOGIN
        // ============================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.MotDePasse))
                throw new Exception("Email and password are required.");

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
                    throw new Exception("Your company account has not been approved yet.");
            }

            return new AuthResponseDto
            {
                Token = _jwtService.GenerateToken(user),
                UserName = user.Nom
            };
        }
    }
}