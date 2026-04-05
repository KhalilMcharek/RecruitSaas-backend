namespace Recrutement_api.Services.Expert;

using Recrutement_api.DTOs.Email;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Shared;
using BC = BCrypt.Net.BCrypt;

public class ExpertService
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;

    public ExpertService(ApplicationDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    private async Task<Guid> ResolveTenantIdAsync(Guid userOrTenantId)
    {
        bool isTenant = await _context.Tenants.AnyAsync(t => t.Id == userOrTenantId);
        if (isTenant) return userOrTenantId;

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.UtilisateurId == userOrTenantId);

        if (tenant == null)
            throw new UnauthorizedAccessException("Tenant introuvable pour cet utilisateur.");

        return tenant.Id;
    }

    // 1. INVITER un expert
    public async Task<ExpertResponseDto> InviteExpertAsync(InviteExpertDto dto, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId && c.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Entreprise introuvable ou acces refuse.");

        bool existe = await _context.Experts
            .AnyAsync(e => e.Email == dto.Email && e.CompanyId == dto.CompanyId);

        if (existe)
            throw new InvalidOperationException($"Un expert avec lemail {dto.Email} existe deja.");

        var tempPassword = GenerateTempPassword();

        var utilisateur = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            Nom = dto.LastName,
            Prenom = dto.FirstName,
            Role = RoleUtilisateur.Expert,
            MotDePasseHash = BC.HashPassword(tempPassword),
            EstActif = true,
            CreeLe = DateTime.UtcNow
        };

        _context.Utilisateurs.Add(utilisateur);

        var expert = new Expert
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            CompanyId = dto.CompanyId,
            TenantId = tenantId,
            IsInvited = true,
            IsActive = true,
            TemporaryPassword = BC.HashPassword(tempPassword),
            UtilisateurId = utilisateur.Id
        };

        _context.Experts.Add(expert);
        await _context.SaveChangesAsync();

        await _emailService.SendExpertInvitationAsync(new ExpertInvitationEmailDto
        {
            ToEmail = dto.Email,
            ToName = $"{dto.FirstName} {dto.LastName}",
            CompanyName = company.Nom,
            InvitedByName = "L'équipe RH",
            InvitationUrl = "http://localhost:5173/login",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            TemporaryPassword = tempPassword
        });

        await _context.Entry(expert).Reference(e => e.Company).LoadAsync();

        return ToDto(expert);
    }

    // 2. LISTE des experts
    public async Task<ExpertListResponseDto> GetExpertsByCompanyAsync(
        Guid companyId, ExpertFilterDto filter, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == tenantId);

        if (company == null)
            throw new KeyNotFoundException("Entreprise introuvable ou accès refusé.");

        var query = _context.Experts
            .Include(e => e.Company)
            .Include(e => e.Utilisateur)
            .Where(e => e.CompanyId == companyId && e.TenantId == tenantId);

        if (filter.IsActive.HasValue)
            query = query.Where(u => u.IsActive == filter.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string s = filter.Search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s));
        }

        int total = await query.CountAsync();

        var items = await query
            .OrderBy(u => u.LastName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new ExpertResponseDto
            {
                Id = u.Id,
                FullName = $"{u.FirstName} {u.LastName}",
                Email = u.Email,
                IsActive = u.IsActive,
                IsInvited = u.IsInvited,
                CompanyName = company.Nom
            })
            .ToListAsync();

        return new ExpertListResponseDto
        {
            Items = items,
            Total = total,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalPages = (int)Math.Ceiling(total / (double)filter.PageSize)
        };
    }

    // 3. DETAIL
    public async Task<ExpertResponseDto> GetExpertByIdAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable");

        return ToDto(expert);
    }

    // 4. UPDATE
    public async Task<ExpertResponseDto> UpdateExpertAsync(Guid id, UpdateExpertDto dto, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable");

        if (expert.Email != dto.Email)
        {
            bool emailPris = await _context.Experts
                .AnyAsync(e => e.Email == dto.Email && e.Id != id);

            if (emailPris)
                throw new InvalidOperationException("Email deja utilise");
        }

        expert.FirstName = dto.FirstName;
        expert.LastName = dto.LastName;
        expert.Email = dto.Email;

        await _context.SaveChangesAsync();

        return ToDto(expert);
    }

    // 5. DESACTIVER
    public async Task DeactivateExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable");

        expert.IsActive = false;
        await _context.SaveChangesAsync();
    }

    // 6. REACTIVER
    public async Task ReactivateExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable");

        expert.IsActive = true;
        await _context.SaveChangesAsync();
    }

    // 7. DELETE
    public async Task DeleteExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable");

        if (expert.UtilisateurId.HasValue)
        {
            var utilisateur = await _context.Utilisateurs.FindAsync(expert.UtilisateurId.Value);
            if (utilisateur != null)
                _context.Utilisateurs.Remove(utilisateur);
        }

        _context.Experts.Remove(expert);
        await _context.SaveChangesAsync();
    }

    private static ExpertResponseDto ToDto(Expert e) => new()
    {
        Id = e.Id,
        FullName = $"{e.FirstName} {e.LastName}",
        Email = e.Email,
        IsActive = e.IsActive,
        IsInvited = e.IsInvited,
        CompanyName = e.Company?.Nom ?? ""
    };

    private static string GenerateTempPassword()
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";
        var random = new Random();

        return new string(Enumerable.Range(0, 10)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }
}