using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Services.Implementations
{
    public class SavedJobService : ISavedJobService
    {
        private readonly ApplicationDbContext _db;

        public SavedJobService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ── GET all saved jobs for a candidate ──────────────────────────────
        public async Task<IEnumerable<SavedJobDto>> GetSavedJobsAsync(Guid candidatId)
        {
            return await _db.SavedJobs
                .Where(s => s.CandidatId == candidatId)
                .Include(s => s.Offre)
                    .ThenInclude(o => o.Entreprise)
                .OrderByDescending(s => s.SavedAt)
                .Select(s => new SavedJobDto
                {
                    Id            = s.Id,
                    OffreId       = s.OffreId,
                    Titre         = s.Offre.Titre,
                    NomEntreprise = s.Offre.Entreprise != null ? s.Offre.Entreprise.Nom : null,
                    Localisation  = s.Offre.Localisation,
                    TypeContrat   = s.Offre.TypeContrat.ToString(),
                    LogoUrl       = s.Offre.Entreprise != null ? s.Offre.Entreprise.LogoUrl : null,
                    CreeLe        = s.Offre.CreeLe,
                    SavedAt       = s.SavedAt
                })
                .ToListAsync();
        }

        // ── SAVE a job ───────────────────────────────────────────────────────
        public async Task<SavedJobDto> SaveJobAsync(Guid candidatId, Guid offreId)
        {
            var existing = await _db.SavedJobs
                .FirstOrDefaultAsync(s => s.CandidatId == candidatId && s.OffreId == offreId);

            if (existing != null)
                return await BuildDto(existing.Id);

            var offreExists = await _db.OffresEmploi.AnyAsync(o => o.Id == offreId);
            if (!offreExists)
                throw new KeyNotFoundException($"Offre {offreId} introuvable.");

            var saved = new SavedJob
            {
                Id         = Guid.NewGuid(),
                CandidatId = candidatId,
                OffreId    = offreId,
                SavedAt    = DateTime.UtcNow
            };

            _db.SavedJobs.Add(saved);
            await _db.SaveChangesAsync();

            return await BuildDto(saved.Id);
        }

        // ── REMOVE a saved job ───────────────────────────────────────────────
        public async Task RemoveSavedJobAsync(Guid candidatId, Guid offreId)
        {
            var saved = await _db.SavedJobs
                .FirstOrDefaultAsync(s => s.CandidatId == candidatId && s.OffreId == offreId);

            if (saved != null)
            {
                _db.SavedJobs.Remove(saved);
                await _db.SaveChangesAsync();
            }
        }

        // ── CHECK if a job is saved ──────────────────────────────────────────
        public async Task<bool> IsJobSavedAsync(Guid candidatId, Guid offreId)
        {
            return await _db.SavedJobs
                .AnyAsync(s => s.CandidatId == candidatId && s.OffreId == offreId);
        }

        // ── Private helper ───────────────────────────────────────────────────
        private async Task<SavedJobDto> BuildDto(Guid savedJobId)
        {
            return await _db.SavedJobs
                .Where(s => s.Id == savedJobId)
                .Include(s => s.Offre)
                    .ThenInclude(o => o.Entreprise)
                .Select(s => new SavedJobDto
                {
                    Id            = s.Id,
                    OffreId       = s.OffreId,
                    Titre         = s.Offre.Titre,
                    NomEntreprise = s.Offre.Entreprise != null ? s.Offre.Entreprise.Nom : null,
                    Localisation  = s.Offre.Localisation,
                    TypeContrat   = s.Offre.TypeContrat.ToString(),
                    LogoUrl       = s.Offre.Entreprise != null ? s.Offre.Entreprise.LogoUrl : null,
                    CreeLe        = s.Offre.CreeLe,
                    SavedAt       = s.SavedAt
                })
                .FirstAsync();
        }
    }
}