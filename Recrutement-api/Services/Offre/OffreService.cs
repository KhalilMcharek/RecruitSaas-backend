using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.DTOs.Offre;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;
using System.Linq;

namespace Recrutement_api.Services
{
    public class OffreService : IOffreService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILinkGeneratorService _linkGenerator;
        public OffreService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ILinkGeneratorService linkGenerator)
        {
            _context = context;
            _currentUser = currentUser;
            _linkGenerator = linkGenerator;
        }

        public async Task<OffreResponseDto> CreerOffreAsync(OffreCreateDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var entreprise = await _context.Entreprises
                .FirstOrDefaultAsync(e => e.Id == dto.EntrepriseId && e.TenantId == tenantId);

            if (entreprise == null)
                throw new Exception("Entreprise introuvable");

            var offre = new OffreEmploi
            {
                Id = Guid.NewGuid(),
                EntrepriseId = dto.EntrepriseId,
                Titre = dto.Titre,
                Description = dto.Description,
                Localisation = dto.Localisation,
                TypeContrat = dto.TypeContrat,
                EstPublie = false,
                CreeLe = DateTime.UtcNow
            };

            offre.LienPublic = _linkGenerator.GenerateOfferLink(offre.Id);
            _context.OffresEmploi.Add(offre);


            await _context.SaveChangesAsync();

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                Description = offre.Description,
                Localisation = offre.Localisation,
                TypeContrat = offre.TypeContrat,
                EstPublie = offre.EstPublie,
                CreeLe = offre.CreeLe,
                LienPublic = offre.LienPublic,
                EntrepriseId = entreprise.Id,
                NomEntreprise = entreprise.Nom,

            };
        }
       /* public async Task<OffreResponseDto> ConfigurerFormulaireAsync(Guid offreId, FormulaireConfigDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Include(o => o.Formulaire)
                    .ThenInclude(f => f.ChampsPersonnalises)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null) throw new Exception("Offre introuvable");

              if (offre.Formulaire == null)
    {
        offre.Formulaire = new FormulaireCandidature
        {
            Id = Guid.NewGuid(),
            OffreId = offreId,
            ChampsPersonnalises = new List<ChampPersonnalise>()
        };

        _context.FormulairesCandidature.Add(offre.Formulaire);
    }

    var formulaire = offre.Formulaire;

    foreach (var champDto in dto.Champs)
    {
        if (champDto.Id.HasValue)
        {
            var champ = formulaire.ChampsPersonnalises
                .FirstOrDefault(c => c.Id == champDto.Id);

            if (champ == null)
                throw new Exception("Champ introuvable");

            champ.Nom = champDto.Nom;
            champ.Question = champDto.Question;
            champ.Type = champDto.Type;
            champ.EstObligatoire = champDto.EstObligatoire;
            champ.Ordre = champDto.Ordre;
        }
        else
        {
            formulaire.ChampsPersonnalises.Add(new ChampPersonnalise
            {
                Id = Guid.NewGuid(),
                Nom = champDto.Nom,
                Question = champDto.Question,
                Type = champDto.Type,
                EstObligatoire = champDto.EstObligatoire,
                Ordre = champDto.Ordre
            });
        }
    }

            // Génération du lien si besoin
            if (string.IsNullOrEmpty(offre.LienPublic))
            {
                offre.LienPublic = _linkGenerator.GenerateOfferLink(offre.Id);
            }

            await _context.SaveChangesAsync();

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                Description = offre.Description,
                Localisation = offre.Localisation,
                TypeContrat = offre.TypeContrat,
                EstPublie = offre.EstPublie,
                CreeLe = offre.CreeLe,
                LienPublic = offre.LienPublic,
                EntrepriseId = offre.Entreprise.Id,
                NomEntreprise = offre.Entreprise.Nom,
                Formulaire = new FormulaireResponseDto
                {
                    Id = offre.Formulaire.Id,
                    Champs = offre.Formulaire.ChampsPersonnalises.Select(c => new ChampPersonnaliseResponseDto
                    {
                        Id = c.Id,
                        Nom = c.Nom,
                        Question = c.Question,
                        Type = c.Type,
                        EstObligatoire = c.EstObligatoire,
                        OptionsJson = c.OptionsJson,
                        Ordre = c.Ordre
                    }).ToList()
                }
            };
        } */
        public async Task<OffreResponseDto> ObtenirOffreParIdAsync(Guid offreId)
        {
            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Include(o => o.Formulaire)
                    .ThenInclude(f => f.ChampsPersonnalises)
                .Include(o => o.Assignations)
                    .ThenInclude(a => a.Expert)
                        .ThenInclude(e => e.Utilisateur)
                .FirstOrDefaultAsync(o => o.Id == offreId);

            if (offre == null)
                throw new Exception("Offre introuvable");

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                Description = offre.Description,
                Localisation = offre.Localisation,
                TypeContrat = offre.TypeContrat,
                EstPublie = offre.EstPublie,
                CreeLe = offre.CreeLe,
                LienPublic = offre.LienPublic,

                EntrepriseId = offre.Entreprise.Id,
                NomEntreprise = offre.Entreprise?.Nom,

                Formulaire = offre.Formulaire == null ? null : new FormulaireResponseDto
                {
                    Id = offre.Formulaire.Id,
                    Champs = offre.Formulaire.ChampsPersonnalises
                        .OrderBy(c => c.Ordre)
                        .Select(c => new ChampPersonnaliseResponseDto
                        {
                            Id = c.Id,
                            Nom = c.Nom,
                            Question = c.Question,
                            Type = c.Type,
                            EstObligatoire = c.EstObligatoire,
                            OptionsJson = c.OptionsJson,
                            Ordre = c.Ordre
                        }).ToList()
                },

               Experts = offre.Assignations?.Select(a => new ExpertDto
{
    Id = a.Expert.Id,
    UtilisateurId = a.Expert.UtilisateurId ?? Guid.Empty,
    Nom = a.Expert.Utilisateur != null ? a.Expert.Utilisateur.Nom : string.Empty,
    EntrepriseId = a.Expert.EntrepriseId ?? Guid.Empty,
    NomEntreprise = offre.Entreprise?.Nom,
    Poste = a.Expert.Poste
}).ToList() ?? new List<ExpertDto>()
            };
        }
        public async Task<List<OffreResponseDto>> ObtenirOffresParTenantAsync(  Guid? entrepriseId, string? search,string? filter)
        {
            var tenantId = _currentUser.TenantId;

            var query = _context.OffresEmploi
                .Where(o => o.Entreprise.TenantId == tenantId)
                .AsQueryable();

            if (entrepriseId.HasValue)
            {
                query = query.Where(o => o.EntrepriseId == entrepriseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var pattern = $"%{term}%";

                    query = query.Where(o =>
                        EF.Functions.Like(o.Titre.ToLower(), pattern));
                }
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                switch (filter.ToLower())
                {
                    case "publie":
                        query = query.Where(o => o.EstPublie);
                        break;

                    case "brouillon":
                        query = query.Where(o => !o.EstPublie);
                        break;
                }
            }

            var offres = await query
                .OrderByDescending(o => o.CreeLe)
                .Select(o => new OffreResponseDto
                {
                    Id = o.Id,
                    Titre = o.Titre,
                    Description = o.Description,
                    Localisation = o.Localisation,
                    TypeContrat = o.TypeContrat,
                    EstPublie = o.EstPublie,
                    CreeLe = o.CreeLe,
                    LienPublic = o.LienPublic,
                    EntrepriseId = o.Entreprise.Id,
                    NomEntreprise = o.Entreprise.Nom
                })
                .ToListAsync();

            return offres;
        }

        public async Task<OffreResponseDto> ChangerStatutPublicationAsync(Guid offreId, bool publier)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null)
                throw new Exception("Offre introuvable");

            offre.EstPublie = publier;

            if (publier && string.IsNullOrWhiteSpace(offre.LienPublic))
            {
                offre.LienPublic = _linkGenerator.GenerateOfferLink(offre.Id);
            }

            await _context.SaveChangesAsync();

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                Description = offre.Description,
                Localisation = offre.Localisation,
                TypeContrat = offre.TypeContrat,
                EstPublie = offre.EstPublie,
                CreeLe = offre.CreeLe,
                LienPublic = offre.LienPublic,
                EntrepriseId = offre.Entreprise.Id,
                NomEntreprise = offre.Entreprise.Nom
            };
        }

        public async Task<OffreResponseDto> AssignerExpertsAsync(Guid offreId, AssignationExpertDto dto)
        {
            var offre = await _context.OffresEmploi
                .Include(o => o.Assignations)
                .Include(o => o.Entreprise) 
                .FirstOrDefaultAsync(o => o.Id == offreId);

            if (offre == null)
                throw new Exception("Offre non trouvée");

            var expertsValides = await _context.Experts
                     .Where(e => dto.ExpertIds.Contains(e.Id)
                              && e.EntrepriseId == offre.EntrepriseId)
                                    .Select(e => e.Id)
                                     .ToListAsync();
            if (!expertsValides.Any())
                throw new Exception("Aucun expert valide appartenant à cette entreprise n'a été trouvé.");

            var expertsDejaAssignes = offre.Assignations
                .Select(a => a.ExpertId)
                .ToHashSet();

            var nouveauxExperts = expertsValides
                .Where(id => !expertsDejaAssignes.Contains(id))
                .ToList();

            foreach (var expertId in nouveauxExperts)
            {
                offre.Assignations.Add(new AssignationExpert
                {
                    //Id = Guid.NewGuid(),
                    OffreId = offreId,
                    ExpertId = expertId
                });
            }
          
            await _context.SaveChangesAsync();

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                EntrepriseId = offre.EntrepriseId,
                NomEntreprise = offre.Entreprise?.Nom
            };
        }

        public async Task<List<ExpertDto>> RechercherExpertsAsync(Guid offreId, string? search)
        {
            var offre = await _context.OffresEmploi
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offreId);

            if (offre == null)
                throw new Exception("Offre non trouvée");

            var query = _context.Experts
                .Where(e => e.EntrepriseId == offre.EntrepriseId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();

                query = query.Where(e =>
                    e.Utilisateur.Nom.ToLower().Contains(search) ||
                    e.Poste.ToLower().Contains(search));
            }

            return await query
                .Select(e => new ExpertDto
                {
                    Id = e.Id,
                    UtilisateurId = e.UtilisateurId ?? Guid.Empty,  
                    Nom = e.Utilisateur != null ? e.Utilisateur.Nom : string.Empty,
                    EntrepriseId = e.EntrepriseId ?? Guid.Empty,    
                    NomEntreprise = e.Entreprise != null ? e.Entreprise.Nom : string.Empty,
                    Poste = e.Poste
                })
                .Take(10)
                .ToListAsync();
        }

        public async Task<OffreResponseDto> ModifierOffreAsync(Guid offreId, OffreUpdateDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null)
                throw new Exception("Offre introuvable ou accès non autorisé");

            // Mise à jour des champs
            offre.Titre = dto.Titre;
            offre.Description = dto.Description;
            offre.Localisation = dto.Localisation;
            
if (dto.TypeContrat.HasValue)
{
    offre.TypeContrat = dto.TypeContrat.Value;
}

            await _context.SaveChangesAsync();

            return new OffreResponseDto
            {
                Id = offre.Id,
                Titre = offre.Titre,
                Description = offre.Description,
                Localisation = offre.Localisation,
                TypeContrat = offre.TypeContrat,
                EstPublie = offre.EstPublie,
                CreeLe = offre.CreeLe,
                LienPublic = offre.LienPublic,
                EntrepriseId = offre.EntrepriseId,
                NomEntreprise = offre.Entreprise.Nom
            };
        }
        public async Task SupprimerAssignationExpertAsync(Guid offreId, Guid expertId)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null)
                throw new Exception("Offre non trouvée");

            var assignation = await _context.AssignationsExperts
                .FirstOrDefaultAsync(a => a.OffreId == offreId && a.ExpertId == expertId);

            if (assignation == null)
                throw new Exception("Assignation non trouvée");

            _context.AssignationsExperts.Remove(assignation);

            await _context.SaveChangesAsync();
        }


        public async Task SupprimerOffreAsync(Guid offreId)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null)
                throw new Exception("Offre introuvable");

            _context.OffresEmploi.Remove(offre);

            await _context.SaveChangesAsync();
        }
        public async Task<FormulaireResponseDto> InitialiserFormulaireAsync(Guid offreId)
        {
            var tenantId = _currentUser.TenantId;

            var offre = await _context.OffresEmploi
                .Include(o => o.Entreprise)
                .Include(o => o.Formulaire)
                .FirstOrDefaultAsync(o => o.Id == offreId && o.Entreprise.TenantId == tenantId);

            if (offre == null)
                throw new Exception("Offre introuvable");

            if (offre.Formulaire == null)
            {
                offre.Formulaire = new FormulaireCandidature
                {
                    Id = Guid.NewGuid(),
                    OffreId = offre.Id
                };

                _context.FormulairesCandidature.Add(offre.Formulaire);
            }

            if (string.IsNullOrEmpty(offre.LienPublic))
            {
                offre.LienPublic = _linkGenerator.GenerateOfferLink(offre.Id);
            }

            await _context.SaveChangesAsync();

            return new FormulaireResponseDto
            {
                Id = offre.Formulaire.Id,
                Champs = new List<ChampPersonnaliseResponseDto>()
            };
        }

        public async Task<List<ChampPersonnaliseResponseDto>> AjouterChampsAsync(Guid offreId, List<ChampPersonnaliseDto> dtos)
        {
            var tenantId = _currentUser.TenantId;

            var formulaire = await _context.FormulairesCandidature
                .Include(f => f.Offre)
                .ThenInclude(o => o.Entreprise)
                .Include(f => f.ChampsPersonnalises)
                .FirstOrDefaultAsync(f => f.OffreId == offreId && f.Offre.Entreprise.TenantId == tenantId);

            if (formulaire == null)
                throw new Exception("Formulaire introuvable");

            var nouveauxChamps = new List<ChampPersonnalise>();

            foreach (var dto in dtos)
            {
                if (formulaire.ChampsPersonnalises.Any(c => c.Ordre == dto.Ordre))
                    throw new Exception($"Un champ avec ordre {dto.Ordre} existe déjà");

                var champ = new ChampPersonnalise
                {
                    Id = Guid.NewGuid(),
                    FormulaireId = formulaire.Id,
                    Nom = dto.Nom,
                    Question = dto.Question,
                    Type = dto.Type,
                    EstObligatoire = dto.EstObligatoire,
                    OptionsJson = dto.OptionsJson,
                    Ordre = dto.Ordre
                };

                nouveauxChamps.Add(champ);
            }

            _context.ChampsPersonnalises.AddRange(nouveauxChamps);

            await _context.SaveChangesAsync();

            return nouveauxChamps.Select(c => new ChampPersonnaliseResponseDto
            {
                Id = c.Id,
                Nom = c.Nom,
                Question = c.Question,
                Type = c.Type,
                EstObligatoire = c.EstObligatoire,
                OptionsJson = c.OptionsJson,
                Ordre = c.Ordre
            }).ToList();
        }
        public async Task<ChampPersonnaliseResponseDto> ModifierChampAsync(Guid champId, ChampPersonnaliseDto dto)
        {
            var tenantId = _currentUser.TenantId;

            var champ = await _context.ChampsPersonnalises
                .Include(c => c.Formulaire)
                .ThenInclude(f => f.Offre)
                .ThenInclude(o => o.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == champId && c.Formulaire.Offre.Entreprise.TenantId == tenantId);

            if (champ == null)
                throw new Exception("Champ introuvable");

            champ.Nom = dto.Nom;
            champ.Question = dto.Question;
            champ.Type = dto.Type;
            champ.EstObligatoire = dto.EstObligatoire;
            champ.OptionsJson = dto.OptionsJson;
            champ.Ordre = dto.Ordre;

            await _context.SaveChangesAsync();

            return new ChampPersonnaliseResponseDto
            {
                Id = champ.Id,
                Nom = champ.Nom,
                Question = champ.Question,
                Type = champ.Type,
                EstObligatoire = champ.EstObligatoire,
                OptionsJson = champ.OptionsJson,
                Ordre = champ.Ordre
            };
        }

        public async Task SupprimerChampAsync(Guid champId)
        {
            var tenantId = _currentUser.TenantId;

            var champ = await _context.ChampsPersonnalises
                .Include(c => c.Formulaire)
                .ThenInclude(f => f.Offre)
                .ThenInclude(o => o.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == champId && c.Formulaire.Offre.Entreprise.TenantId == tenantId);

            if (champ == null)
                throw new Exception("Champ introuvable");

            _context.ChampsPersonnalises.Remove(champ);

            await _context.SaveChangesAsync();
        }

        public async Task ModifierOrdreChampsAsync(Guid formulaireId, List<ModifierOrdreChampDto> dtos)
        {
            var tenantId = _currentUser.TenantId;

            var formulaire = await _context.FormulairesCandidature
                .Include(f => f.Offre)
                .ThenInclude(o => o.Entreprise)
                .Include(f => f.ChampsPersonnalises)
                .FirstOrDefaultAsync(f =>
                    f.Id == formulaireId &&
                    f.Offre.Entreprise.TenantId == tenantId);

            if (formulaire == null)
                throw new Exception("Formulaire introuvable");

            var champs = formulaire.ChampsPersonnalises;

            foreach (var dto in dtos)
            {
                var champ = champs.FirstOrDefault(c => c.Id == dto.ChampId);

                if (champ == null)
                    throw new Exception($"Champ {dto.ChampId} introuvable");

                champ.Ordre = dto.Ordre;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Entreprise>> GetTenantEntreprisesAsync()
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




