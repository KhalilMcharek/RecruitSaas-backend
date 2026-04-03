using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Services.TenantServices
{
    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuthService _authService;

        public TeamService(ApplicationDbContext context, ICurrentUserService currentUser, IAuthService authService)
        {
            _context = context;
            _currentUser = currentUser;
            _authService = authService;
        }

        public async Task<List<ExpertDto>> GetExpertsAsync(Guid? entrepriseId, string? search)
        {
            var tenantId = _currentUser.TenantId;

            var query = _context.Experts
                .Where(e => e.Entreprise != null && e.Entreprise.TenantId == tenantId)
                .AsQueryable();

            if (entrepriseId.HasValue)
                query = query.Where(e => e.EntrepriseId == entrepriseId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e =>
                    (e.Utilisateur != null && e.Utilisateur.Nom.ToLower().Contains(term)) ||
                    (e.Poste != null && e.Poste.ToLower().Contains(term)));
            }

            return await query
                .OrderBy(e => e.Utilisateur != null ? e.Utilisateur.Nom : string.Empty)
                .Select(e => new ExpertDto
                {
                    Id = e.Id,
                    UtilisateurId = e.UtilisateurId ?? Guid.Empty,
                    Nom = e.Utilisateur != null ? e.Utilisateur.Nom : string.Empty,
                    EntrepriseId = e.EntrepriseId ?? Guid.Empty,
                    NomEntreprise = e.Entreprise != null ? e.Entreprise.Nom : string.Empty,
                    Poste = e.Poste
                })
                .ToListAsync();
        }

        public async Task<ExpertDto> GetExpertByIdAsync(Guid id)
        {
            var tenantId = _currentUser.TenantId;

            var expert = await _context.Experts
                .Where(e => e.Id == id && e.Entreprise != null && e.Entreprise.TenantId == tenantId)
                .Select(e => new ExpertDto
                {
                    Id = e.Id,
                    UtilisateurId = e.UtilisateurId ?? Guid.Empty,
                    Nom = e.Utilisateur != null ? e.Utilisateur.Nom : string.Empty,
                    EntrepriseId = e.EntrepriseId ?? Guid.Empty,
                    NomEntreprise = e.Entreprise != null ? e.Entreprise.Nom : string.Empty,
                    Poste = e.Poste
                })
                .FirstOrDefaultAsync();

            if (expert == null)
                throw new Exception("Expert introuvable");

            return expert;
        }

        public async Task<ExpertDto> CreateExpertAsync(ExpertCreateDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var entreprise = await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Id == dto.EntrepriseId && e.TenantId == tenantId);

            if (entreprise == null)
                throw new Exception("Entreprise introuvable ou non autorisée");

            var emailExists = await _context.Utilisateurs.AnyAsync(u => u.Email == dto.Email);
            if (emailExists)
                throw new Exception("Un utilisateur avec cet email existe déjà.");

            var utilisateur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                Nom = dto.Nom,
                Prenom = dto.Prenom,
                Role = RoleUtilisateur.Expert,
                MotDePasseHash = _authService.HashPassword(dto.MotDePasse),
                CreeLe = DateTime.UtcNow
            };
            await _context.Utilisateurs.AddAsync(utilisateur);

           var expert = new Recrutement_api.Models.Expert
{
    Id = Guid.NewGuid(),
    UtilisateurId = utilisateur.Id,
    EntrepriseId = dto.EntrepriseId,
    CompanyId = dto.EntrepriseId,
    TenantId = entreprise.TenantId,
    Poste = dto.Poste,
    CreeLe = DateTime.UtcNow
};

            _context.Experts.Add(expert);
            await _context.SaveChangesAsync();

            return new ExpertDto
            {
                Id = expert.Id,
                UtilisateurId = expert.UtilisateurId ?? Guid.Empty,
                Nom = utilisateur.Nom,
                EntrepriseId = expert.EntrepriseId ?? Guid.Empty,
                NomEntreprise = entreprise.Nom,
                Poste = expert.Poste
            };
        }

        public async Task<ExpertDto> UpdateExpertAsync(Guid id, ExpertUpdateDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var expert = await _context.Experts
                .Include(e => e.Utilisateur)
                .Include(e => e.Entreprise)
                .FirstOrDefaultAsync(e => e.Id == id && e.Entreprise != null && e.Entreprise.TenantId == tenantId);

            if (expert == null)
                throw new Exception("Expert introuvable ou accès non autorisé");

            expert.Poste = dto.Poste;
            await _context.SaveChangesAsync();

            return new ExpertDto
            {
                Id = expert.Id,
                UtilisateurId = expert.UtilisateurId ?? Guid.Empty,
                Nom = expert.Utilisateur != null ? expert.Utilisateur.Nom : string.Empty,
                EntrepriseId = expert.EntrepriseId ?? Guid.Empty,
                NomEntreprise = expert.Entreprise != null ? expert.Entreprise.Nom : string.Empty,
                Poste = expert.Poste
            };
        }

        public async Task DeleteExpertAsync(Guid id)
        {
            var tenantId = _currentUser.TenantId;

            var expert = await _context.Experts
                .Include(e => e.Entreprise)
                .FirstOrDefaultAsync(e => e.Id == id && e.Entreprise != null && e.Entreprise.TenantId == tenantId);

            if (expert == null)
                throw new Exception("Expert introuvable ou accès non autorisé");

            _context.Experts.Remove(expert);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Entreprise>> GetEntreprisesAsync()
        {
            var tenantId = _currentUser.TenantId;

            return await _context.Entreprises
                .Where(e => e.TenantId == tenantId)
                .Select(e => new Entreprise
                {
                    Id = e.Id,
                    Nom = e.Nom
                })
                .ToListAsync();
        }
    }
}