namespace Recrutement_api.Services.Expert;

using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

public class ExpertUserService : IExpertService
{
    private readonly ApplicationDbContext _context;

    public ExpertUserService(ApplicationDbContext context)
    {
        _context = context;
    }

    //  Offres assignées à l'expert 
    public async Task<List<OffreAssigneeDto>> GetOffresAssigneesAsync(Guid expertId)
    {
        var assignations = await _context.AssignationsExperts
            .Include(a => a.Offre)
                .ThenInclude(o => o.Candidatures)
            .Where(a => a.ExpertId == expertId)
            .ToListAsync();

        return assignations.Select(a => new OffreAssigneeDto
        {
            OffreId = a.OffreId,
            Titre = a.Offre?.Titre ?? "—",
            NombreCandidatures = a.Offre?.Candidatures?.Count ?? 0
        }).ToList();
    }

    //  Candidatures des offres assignées 
    public async Task<List<CandidatureExpertDto>> GetCandidaturesAsync(
        Guid expertId,
        Guid? filtreOffreId = null,
        string? filtreStatut = null)
    {
        // IDs des offres assignées à cet expert
        var offreIds = await _context.AssignationsExperts
            .Where(a => a.ExpertId == expertId)
            .Select(a => a.OffreId)
            .ToListAsync();

        var query = _context.Candidatures
     .Include(c => c.Candidat)
         .ThenInclude(candidat => candidat.Utilisateur)   
     .Include(c => c.Offre)
     .Include(c => c.Avis.Where(av => av.ExpertId == expertId))
     .Where(c => offreIds.Contains(c.OffreId));

        if (filtreOffreId.HasValue)
            query = query.Where(c => c.OffreId == filtreOffreId.Value);

        if (!string.IsNullOrWhiteSpace(filtreStatut))
            query = query.Where(c => c.Statut == filtreStatut);

        var list = await query.OrderByDescending(c => c.CreeLe).ToListAsync();

        return list.Select(c =>
        {
            var avis = c.Avis.FirstOrDefault();

            List<FormulaireReponseDto> responses = new();
            if (!string.IsNullOrWhiteSpace(c.FormulaireResponses))
            {
                try
                {
                    responses = System.Text.Json.JsonSerializer
                        .Deserialize<List<FormulaireReponseDto>>(
                            c.FormulaireResponses,
                            new System.Text.Json.JsonSerializerOptions
                            { PropertyNameCaseInsensitive = true }
                        ) ?? new();
                }
                catch { /* JSON malformé : on ignore */ }
            }

            return new CandidatureExpertDto
            {
                Id = c.Id,
                OffreId = c.OffreId,
                OffreTitre = c.Offre?.Titre ?? "—",
                CandidatId = c.CandidatId,
                CandidatNomComplet = $"{c.Candidat?.Utilisateur?.Prenom} {c.Candidat?.Utilisateur?.Nom}".Trim(),
                CandidatEmail = c.Candidat?.Utilisateur?.Email ?? "",
                CandidatTelephone = c.Candidat?.Telephone ?? "",
                CvUrl = c.CvUrl ?? "",
                Statut = c.Statut ?? "",
                CreeLe = c.CreeLe,
                FormulaireResponses = responses,
                AvisExpert = avis == null ? null : new AvisExpertDetailDto
                {
                    Id = avis.Id,
                    Score = avis.Score,
                    Commentaire = avis.Commentaire ?? "",
                    CreeLe = avis.CreeLe
                }
            };
        }).ToList();
    }

    //  Détail d'une candidature 
    public async Task<CandidatureExpertDto> GetCandidatureDetailAsync(
        Guid expertId, Guid candidatureId)
    {
        var offreIds = await _context.AssignationsExperts
            .Where(a => a.ExpertId == expertId)
            .Select(a => a.OffreId)
            .ToListAsync();

        var c = await _context.Candidatures
    .Include(c => c.Candidat)
        .ThenInclude(candidat => candidat.Utilisateur)   
    .Include(c => c.Offre)
    .Include(c => c.Avis.Where(av => av.ExpertId == expertId))
    .FirstOrDefaultAsync(c => c.Id == candidatureId
                           && offreIds.Contains(c.OffreId))
    ?? throw new KeyNotFoundException("Candidature introuvable ou accès refusé.");

        var avis = c.Avis.FirstOrDefault();

        return new CandidatureExpertDto
        {
            Id = c.Id,
            OffreId = c.OffreId,
            OffreTitre = c.Offre?.Titre ?? "—",
            CandidatId = c.CandidatId,
            CandidatNomComplet = $"{c.Candidat?.Utilisateur?.Prenom} {c.Candidat?.Utilisateur?.Nom}".Trim(),
            CandidatEmail = c.Candidat?.Utilisateur?.Email ?? "",
            CandidatTelephone = c.Candidat?.Telephone ?? "",
            CvUrl = c.CvUrl ?? "",
            Statut = c.Statut ?? "",
            CreeLe = c.CreeLe,
            FormulaireResponses = new(),
            AvisExpert = avis == null ? null : new AvisExpertDetailDto
            {
                Id = avis.Id,
                Score = avis.Score,
                Commentaire = avis.Commentaire ?? "",
                CreeLe = avis.CreeLe
            }
        };
    }

    // Soumettre / mettre à jour un avis 
    public async Task<AvisExpertDetailDto> SoumettreAvisAsync(
        Guid expertId, SoumettreAvisDto dto)
    {
        bool estAssigne = await _context.AssignationsExperts
            .AnyAsync(a => a.ExpertId == expertId &&
                           _context.Candidatures
                               .Any(c => c.Id == dto.CandidatureId
                                      && c.OffreId == a.OffreId));

        if (!estAssigne)
            throw new UnauthorizedAccessException("Accès refusé à cette candidature.");

        var avis = await _context.AvisExperts
            .FirstOrDefaultAsync(a => a.ExpertId == expertId
                                   && a.CandidatureId == dto.CandidatureId);

        if (avis != null)
        {
            avis.Score = dto.Score;
            avis.Commentaire = dto.Commentaire;
        }
        else
        {
            avis = new AvisExpert
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                CandidatureId = dto.CandidatureId,
                Score = dto.Score,
                Commentaire = dto.Commentaire,
                CreeLe = DateTime.UtcNow
            };
            _context.AvisExperts.Add(avis);
        }

        await _context.SaveChangesAsync();

        return new AvisExpertDetailDto
        {
            Id = avis.Id,
            Score = avis.Score,
            Commentaire = avis.Commentaire ?? "",
            CreeLe = avis.CreeLe
        };
    }
    public async Task<ProfilExpertDto> GetProfilAsync(Guid expertId)
    {
        var expert = await _context.Experts
            .Include(e => e.Utilisateur)
            .FirstOrDefaultAsync(e => e.Id == expertId)
            ?? throw new KeyNotFoundException("Expert introuvable.");

        return new ProfilExpertDto
        {
            Id = expert.Id,
            FirstName = expert.FirstName,
            LastName = expert.LastName,
            Email = expert.Email,
            Phone = expert.Utilisateur?.Telephone,
            Specialty = expert.Poste
        };
    }

    public async Task<ProfilExpertDto> UpdateProfilAsync(Guid expertId, UpdateProfilExpertDto dto)
    {
        var expert = await _context.Experts
            .Include(e => e.Utilisateur)
            .FirstOrDefaultAsync(e => e.Id == expertId)
            ?? throw new KeyNotFoundException("Expert introuvable.");

        expert.FirstName = dto.FirstName;
        expert.LastName = dto.LastName;
        expert.Email = dto.Email;
        expert.Poste = dto.Specialty;

        if (expert.Utilisateur != null)
        {
            expert.Utilisateur.Prenom = dto.FirstName;
            expert.Utilisateur.Nom = dto.LastName;
            expert.Utilisateur.Email = dto.Email;
            expert.Utilisateur.Telephone = dto.Phone;
        }

        await _context.SaveChangesAsync();

        return new ProfilExpertDto
        {
            Id = expert.Id,
            FirstName = expert.FirstName,
            LastName = expert.LastName,
            Email = expert.Email,
            Phone = expert.Utilisateur?.Telephone,
            Specialty = expert.Poste
        };
    }
}