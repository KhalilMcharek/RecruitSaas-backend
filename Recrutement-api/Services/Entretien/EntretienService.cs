using Microsoft.EntityFrameworkCore;
using Recrutement_api.DTOs.Entretien;
using Recrutement_api.Data;
using Recrutement_api.Models;
using System.Text.Json;

namespace Recrutement_api.Services.Entretien
{
    public class EntretienService : IEntretienService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EntretienService> _logger;
        private readonly IConfiguration _config;
        private readonly Recrutement_api.Services.Shared.EmailService _emailService;

        public EntretienService(
            ApplicationDbContext db,
            ILogger<EntretienService> logger,
            IConfiguration config,
            Recrutement_api.Services.Shared.EmailService emailService)
        {
            _db = db;
            _logger = logger;
            _config = config;
            _emailService = emailService;
        }

        // ── Planifier : crée l'entretien et envoie le lien au candidat ──────────
        public async Task<object> PlanifierAsync(Guid candidatureId, PlanifierDto dto)
        {
            var candidature = await _db.Candidatures
                .Include(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.Offre).ThenInclude(o => o.Entreprise)
                .FirstOrDefaultAsync(c => c.Id == candidatureId)
                ?? throw new Exception("Candidature introuvable");

            // Vérifie ou crée l'entretien
            var entretien = await _db.EntretiensIA
                .FirstOrDefaultAsync(e => e.CandidatureId == candidatureId);

            if (entretien == null)
            {
                entretien = new EntretienIA
                {
                    Id = Guid.NewGuid(),
                    CandidatureId = candidatureId,
                    Statut = "LienEnvoye",
                    CreeLe = DateTime.UtcNow
                };
                _db.EntretiensIA.Add(entretien);
            }

            // Génère un token unique sécurisé
            entretien.LienToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                                              .Replace("+", "-").Replace("/", "_").Replace("=", "");
            entretien.CreneauxDisponibles = JsonSerializer.Serialize(
                dto.Creneaux.Select(d => d.ToUniversalTime()).ToList());
            entretien.Statut = "LienEnvoye";
            entretien.MisAJourLe = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // URL publique de scheduling
            var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:5173";
            var schedulingUrl = $"{frontendUrl}/interview/{entretien.LienToken}";

            _logger.LogInformation(
                "[Entretien] Lien créé pour candidature {Id} : {Url}",
                candidatureId, schedulingUrl);

            // Envoi de l'email au candidat
            try
            {
                var nomCandidat = candidature.Candidat.Utilisateur.Nom;
                var prenomCandidat = candidature.Candidat.Utilisateur.Prenom ?? "";
                var nomComplet = $"{prenomCandidat} {nomCandidat}".Trim();

                await _emailService.SendInterviewInviteAsync(
                    candidature.Candidat.Utilisateur.Email,
                    nomComplet,
                    candidature.Offre.Titre,
                    candidature.Offre.Entreprise?.Nom ?? "RecruitSaaS",
                    schedulingUrl,
                    dto.Message);

                _logger.LogInformation(
                    "[Entretien] Email envoyé à {Email}",
                    candidature.Candidat.Utilisateur.Email);
            }
            catch (Exception ex)
            {
                // L'email échoue silencieusement — le lien est quand même retourné
                _logger.LogWarning("[Entretien] Email non envoyé : {Err}", ex.Message);
            }

            return new
            {
                entretien.Id,
                entretien.Statut,
                entretien.LienToken,
                SchedulingUrl = schedulingUrl,
                Creneaux = dto.Creneaux,
                NomCandidat = candidature.Candidat.Utilisateur.Nom,
                EmailCandidat = candidature.Candidat.Utilisateur.Email,
                TitreOffre = candidature.Offre.Titre
            };
        }

        // ── Liste tous les entretiens du tenant ───────────────────────────────────
        public async Task<List<object>> GetAllAsync(Guid? offreId, string? statut)
        {
            var query = _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                .AsQueryable();

            if (offreId.HasValue)
                query = query.Where(e => e.Candidature.OffreId == offreId.Value);
            if (!string.IsNullOrWhiteSpace(statut))
                query = query.Where(e => e.Statut == statut);

            var list = await query.OrderByDescending(e => e.CreeLe).ToListAsync();

            return list.Select(e => (object)new
            {
                e.Id,
                e.Statut,
                e.DateScheduled,
                e.LienActif,
                e.CreeLe,
                NomCandidat = e.Candidature.Candidat.Utilisateur.Nom,
                EmailCandidat = e.Candidature.Candidat.Utilisateur.Email,
                TitreOffre = e.Candidature.Offre.Titre,
                CandidatureId = e.CandidatureId
            }).ToList();
        }

        // ── Détail entretien ──────────────────────────────────────────────────────
        public async Task<object?> GetByIdAsync(Guid id)
        {
            var e = await _db.EntretiensIA
                .Include(x => x.Candidature)
                    .ThenInclude(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(x => x.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (e == null) return null;

            List<DateTime> creneaux = new();
            if (!string.IsNullOrEmpty(e.CreneauxDisponibles))
            {
                try { creneaux = JsonSerializer.Deserialize<List<DateTime>>(e.CreneauxDisponibles) ?? new(); }
                catch { }
            }

            return new
            {
                e.Id,
                e.Statut,
                e.DateScheduled,
                e.LienActif,
                e.Score,
                e.Transcript,
                e.CreeLe,
                CreneauxDisponibles = creneaux,
                NomCandidat = e.Candidature.Candidat.Utilisateur.Nom,
                EmailCandidat = e.Candidature.Candidat.Utilisateur.Email,
                TitreOffre = e.Candidature.Offre.Titre,
                CandidatureId = e.CandidatureId
            };
        }

        // ── Annuler ───────────────────────────────────────────────────────────────
        public async Task AnnulerAsync(Guid id)
        {
            var entretien = await _db.EntretiensIA.FindAsync(id)
                ?? throw new Exception("Entretien introuvable");

            entretien.Statut = "Annule";
            entretien.MisAJourLe = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ── PUBLIC : créneaux disponibles via token ───────────────────────────────
        public async Task<object?> GetCreneauxByTokenAsync(string token)
        {
            var entretien = await _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(e => e.LienToken == token);

            if (entretien == null || entretien.Statut == "Annule") return null;

            List<DateTime> creneaux = new();
            if (!string.IsNullOrEmpty(entretien.CreneauxDisponibles))
            {
                try { creneaux = JsonSerializer.Deserialize<List<DateTime>>(entretien.CreneauxDisponibles) ?? new(); }
                catch { }
            }

            return new
            {
                entretien.Id,
                entretien.Statut,
                entretien.DateScheduled,
                CreneauxDisponibles = creneaux,
                TitreOffre = entretien.Candidature.Offre.Titre,
                NomEntreprise = entretien.Candidature.Offre.Entreprise?.Nom
            };
        }

        // ── PUBLIC : candidat confirme son créneau ────────────────────────────────
        public async Task<object?> ConfirmerCreneauAsync(string token, DateTime dateChoisie)
        {
            var entretien = await _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(e => e.LienToken == token);

            if (entretien == null || entretien.Statut == "Annule") return null;

            // Vérifie que le créneau choisi est dans les créneaux disponibles
            List<DateTime> creneaux = new();
            if (!string.IsNullOrEmpty(entretien.CreneauxDisponibles))
            {
                try { creneaux = JsonSerializer.Deserialize<List<DateTime>>(entretien.CreneauxDisponibles) ?? new(); }
                catch { }
            }

            var dateUTC = dateChoisie.ToUniversalTime();
            bool valide = creneaux.Any(c => Math.Abs((c - dateUTC).TotalMinutes) < 1);
            if (!valide) return null;

            entretien.DateScheduled = dateUTC;
            entretien.Statut = "Planifie";
            entretien.MisAJourLe = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Entretien] Candidat {Nom} a choisi le créneau {Date}",
                entretien.Candidature.Candidat.Utilisateur.Nom, dateUTC);

            var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:5173";
            var entretienUrl = $"{frontendUrl}/interview/{token}/rejoindre";

            return new
            {
                entretien.Id,
                entretien.Statut,
                DateScheduled = dateUTC,
                EntretienUrl = entretienUrl,
                TitreOffre = entretien.Candidature.Offre.Titre,
                NomCandidat = entretien.Candidature.Candidat.Utilisateur.Nom,
                message = $"Entretien confirmé le {dateUTC:dd/MM/yyyy à HH:mm} UTC"
            };
        }

        // ── PUBLIC : rejoindre l'entretien (actif seulement à l'heure J) ─────────
        public async Task<EntretienRejoindreResult?> RejoindreAsync(string token)
        {
            var entretien = await _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(e => e.LienToken == token);

            if (entretien == null || entretien.Statut == "Annule") return null;

            // Le lien est actif 5 minutes avant et pendant l'entretien (30 min)
            var now = DateTime.UtcNow;
            bool estActif = entretien.DateScheduled.HasValue
                && now >= entretien.DateScheduled.Value.AddMinutes(-5)
                && now <= entretien.DateScheduled.Value.AddMinutes(35);

            if (estActif && entretien.Statut == "Planifie")
            {
                entretien.Statut = "EnCours";
                entretien.LienActif = true;
                entretien.MisAJourLe = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // URL de l'avatar IA (page entretien virtuel)
            var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:5173";
            var avatarUrl = $"{frontendUrl}/interview/{token}/live";

            return new EntretienRejoindreResult
            {
                EstActif = estActif,
                DateScheduled = entretien.DateScheduled,
                LienEntretien = estActif ? avatarUrl : null,
                NomCandidat = entretien.Candidature.Candidat.Utilisateur.Nom,
                TitreOffre = entretien.Candidature.Offre.Titre
            };
        }
    }
}