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

    public async Task<ExpertResponseDto> InviteExpertAsync(InviteExpertDto dto, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId && c.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Entreprise introuvable ou acces refuse.");

        bool existe = await _context.Experts
            .AnyAsync(e => e.Email == dto.Email && e.CompanyId == dto.CompanyId);
        if (existe)
            throw new InvalidOperationException(
                $"Un expert avec lemail {dto.Email} existe deja dans cette entreprise.");

        var tempPassword = GenerateTempPassword();

        var expert = new Expert
        {
            FirstName         = dto.FirstName,
            LastName          = dto.LastName,
            Email             = dto.Email,
            CompanyId         = dto.CompanyId,
            TenantId          = tenantId,
            IsInvited         = true,
            IsActive          = false,
            TemporaryPassword = BC.HashPassword(tempPassword)
        };

        _context.Experts.Add(expert);
        await _context.SaveChangesAsync();

        await _emailService.SendExpertInvitationAsync(new ExpertInvitationEmailDto
        {
            ToEmail           = dto.Email,
            ToName            = $"{dto.FirstName} {dto.LastName}",
            CompanyName       = company.Nom,
            InvitedByName     = "L'équipe RH",
            InvitationUrl     = "http://localhost:5173/login",
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            TemporaryPassword = tempPassword
        });

        await _context.Entry(expert).Reference(e => e.Company).LoadAsync();
        return ToDto(expert);
    }

    public async Task<ExpertListResponseDto> GetExpertsByCompanyAsync(
        Guid companyId, ExpertFilterDto filter, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        bool ok = await _context.Companies
            .AnyAsync(c => c.Id == companyId && c.TenantId == tenantId);
        if (!ok)
            throw new KeyNotFoundException("Entreprise introuvable ou acces refuse.");

        var query = _context.Experts
            .Include(e => e.Company)
            .Where(e => e.CompanyId == companyId);

        if (filter.IsActive.HasValue)
            query = query.Where(e => e.IsActive == filter.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string s = filter.Search.ToLower();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(s) ||
                e.LastName.ToLower().Contains(s)  ||
                e.Email.ToLower().Contains(s));
        }

        int total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.LastName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => ToDto(e))
            .ToListAsync();

        return new ExpertListResponseDto
        {
            Items      = items,
            Total      = total,
            Page       = filter.Page,
            PageSize   = filter.PageSize,
            TotalPages = (int)Math.Ceiling(total / (double)filter.PageSize)
        };
    }

    public async Task<ExpertResponseDto> GetExpertByIdAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.Company.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable ou acces refuse.");

        return ToDto(expert);
    }

    public async Task<ExpertResponseDto> UpdateExpertAsync(Guid id, UpdateExpertDto dto, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.Company.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable ou acces refuse.");

        if (expert.Email != dto.Email)
        {
            bool emailPris = await _context.Experts
                .AnyAsync(e => e.Email == dto.Email && e.Id != id);
            if (emailPris)
                throw new InvalidOperationException($"Lemail {dto.Email} est deja utilise.");
        }

        expert.FirstName = dto.FirstName;
        expert.LastName  = dto.LastName;
        expert.Email     = dto.Email;
        await _context.SaveChangesAsync();

        return ToDto(expert);
    }

    public async Task DeactivateExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.Company.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable ou acces refuse.");

        if (!expert.IsActive)
            throw new InvalidOperationException("Lexpert est deja desactive.");

        expert.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task ReactivateExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.Company.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable ou acces refuse.");

        if (expert.IsActive)
            throw new InvalidOperationException("Lexpert est deja actif.");

        expert.IsActive = true;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpertAsync(Guid id, Guid userOrTenantId)
    {
        var tenantId = await ResolveTenantIdAsync(userOrTenantId);

        var expert = await _context.Experts
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && e.Company.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Expert introuvable ou acces refuse.");

        _context.Experts.Remove(expert);
        await _context.SaveChangesAsync();
    }

    private static ExpertResponseDto ToDto(Expert e) => new()
    {
        Id          = e.Id,
        FullName    = $"{e.FirstName} {e.LastName}",
        Email       = e.Email,
        IsActive    = e.IsActive,
        IsInvited   = e.IsInvited,
        CompanyName = e.Company?.Nom ?? string.Empty
    };

    private static string GenerateTempPassword()
    {
        var chars  = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";
        var random = new Random();
        return new string(Enumerable.Range(0, 10)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }
}