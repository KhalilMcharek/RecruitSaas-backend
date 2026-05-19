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
            _db           = db;
            _logger       = logger;
            _config       = config;
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
                    Id             = Guid.NewGuid(),
                    CandidatureId  = candidatureId,
                    Statut         = "Pending Confirmation",
                    CreeLe         = DateTime.UtcNow
                };
                _db.EntretiensIA.Add(entretien);
            }

            // Génère un token unique sécurisé
            entretien.LienToken           = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                                              .Replace("+", "-").Replace("/", "_").Replace("=", "");
            entretien.CreneauxDisponibles = JsonSerializer.Serialize(
                dto.Creneaux.Select(d => d.ToUniversalTime()).ToList());
            entretien.Statut              = "Pending Confirmation";
            entretien.QuestionsJson       = dto.QuestionsJson;
            entretien.Langue              = dto.Langue ?? "FR";
            entretien.MisAJourLe          = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // URL publique de scheduling
            var frontendUrl = _config["Frontend:Url"] ?? "http://localhost:5173";
            var schedulingUrl = $"{frontendUrl}/schedule-interview/{entretien.LienToken}";

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
                SchedulingUrl  = schedulingUrl,
                Creneaux       = dto.Creneaux,
                NomCandidat    = candidature.Candidat.Utilisateur.Nom,
                EmailCandidat  = candidature.Candidat.Utilisateur.Email,
                TitreOffre     = candidature.Offre.Titre
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
                NomCandidat  = e.Candidature.Candidat.Utilisateur.Nom,
                EmailCandidat = e.Candidature.Candidat.Utilisateur.Email,
                TitreOffre   = e.Candidature.Offre.Titre,
                CandidatureId = e.CandidatureId
            }).ToList();
        }

        // ── Détail entretien par CandidatureId ────────────────────────────────────
        public async Task<object?> GetByCandidatureIdAsync(Guid candidatureId)
        {
            var e = await _db.EntretiensIA
                .Include(x => x.Candidature)
                    .ThenInclude(c => c.Candidat).ThenInclude(ca => ca.Utilisateur)
                .Include(x => x.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(x => x.CandidatureId == candidatureId);

            if (e == null) return null;

            List<DateTime> creneaux = new();
            if (!string.IsNullOrEmpty(e.CreneauxDisponibles))
            {
                try { creneaux = JsonSerializer.Deserialize<List<DateTime>>(e.CreneauxDisponibles) ?? new(); }
                catch { }
            }

            List<string> strengths = new();
            List<string> weaknesses = new();
            if (!string.IsNullOrEmpty(e.Strengths)) { try { strengths = JsonSerializer.Deserialize<List<string>>(e.Strengths) ?? new(); } catch { } }
            if (!string.IsNullOrEmpty(e.Weaknesses)) { try { weaknesses = JsonSerializer.Deserialize<List<string>>(e.Weaknesses) ?? new(); } catch { } }

            return new
            {
                e.Id,
                e.Statut,
                e.DateScheduled,
                e.LienActif,
                e.Score,
                e.Summary,
                Strengths = strengths,
                Weaknesses = weaknesses,
                e.Transcript,
                e.CreeLe,
                CreneauxDisponibles = creneaux,
                NomCandidat    = e.Candidature.Candidat.Utilisateur.Nom,
                EmailCandidat  = e.Candidature.Candidat.Utilisateur.Email,
                TitreOffre     = e.Candidature.Offre.Titre,
                CandidatureId  = e.CandidatureId
            };
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

            List<string> strengths = new();
            List<string> weaknesses = new();
            if (!string.IsNullOrEmpty(e.Strengths)) { try { strengths = JsonSerializer.Deserialize<List<string>>(e.Strengths) ?? new(); } catch { } }
            if (!string.IsNullOrEmpty(e.Weaknesses)) { try { weaknesses = JsonSerializer.Deserialize<List<string>>(e.Weaknesses) ?? new(); } catch { } }

            return new
            {
                e.Id,
                e.Statut,
                e.DateScheduled,
                e.LienActif,
                e.Score,
                e.Summary,
                Strengths = strengths,
                Weaknesses = weaknesses,
                e.Transcript,
                e.CreeLe,
                CreneauxDisponibles = creneaux,
                NomCandidat    = e.Candidature.Candidat.Utilisateur.Nom,
                EmailCandidat  = e.Candidature.Candidat.Utilisateur.Email,
                TitreOffre     = e.Candidature.Offre.Titre,
                CandidatureId  = e.CandidatureId
            };
        }

        // ── Annuler ───────────────────────────────────────────────────────────────
        public async Task AnnulerAsync(Guid id)
        {
            var entretien = await _db.EntretiensIA.FindAsync(id)
                ?? throw new Exception("Entretien introuvable");

            entretien.Statut     = "Cancelled";
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

            if (entretien == null || entretien.Statut == "Cancelled") return null;

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

            if (entretien == null || entretien.Statut == "Cancelled") return null;

            // Vérifie que le créneau choisi est dans les créneaux disponibles
            List<DateTime> creneaux = new();
            if (!string.IsNullOrEmpty(entretien.CreneauxDisponibles))
            {
                try { creneaux = JsonSerializer.Deserialize<List<DateTime>>(entretien.CreneauxDisponibles) ?? new(); }
                catch { }
            }

            var dateUTC = dateChoisie.ToUniversalTime();
            
            // Vérifie la capacité (max 2)
            var count = await _db.EntretiensIA
                .CountAsync(e => e.DateScheduled == dateUTC && e.Statut != "Cancelled" && e.Statut != "Abandoned");
            
            if (count >= 2) throw new Exception("Ce créneau est complet (max 2 entretiens).");

            bool valide = creneaux.Any(c => Math.Abs((c - dateUTC).TotalMinutes) < 1);
            if (!valide) return null;

            entretien.DateScheduled = dateUTC;
            entretien.Statut        = "Confirmed";
            entretien.MisAJourLe    = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Entretien] Candidat {Nom} a choisi le créneau {Date}",
                entretien.Candidature.Candidat.Utilisateur.Nom, dateUTC);

            var frontendUrl   = _config["Frontend:Url"] ?? "http://localhost:5173";
            var entretienUrl  = $"{frontendUrl}/entretien-ia?token={Uri.EscapeDataString(token)}";

            return new
            {
                entretien.Id,
                entretien.Statut,
                DateScheduled  = dateUTC,
                EntretienUrl   = entretienUrl,
                TitreOffre     = entretien.Candidature.Offre.Titre,
                NomCandidat    = entretien.Candidature.Candidat.Utilisateur.Nom,
                message        = $"Entretien confirmé le {dateUTC:dd/MM/yyyy à HH:mm} UTC"
            };
        }

        // ── CANDIDAT : ses propres entretiens ────────────────────────────────────
        public async Task<List<object>> GetEntretiensByCandidatAsync(Guid userId)
        {
            // userId peut être l'UserId ou le CandidatId — on cherche par les deux
            var entretiens = await _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                        .ThenInclude(o => o.Entreprise)
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Candidat)
                        .ThenInclude(ca => ca.Utilisateur)
                .Where(e =>
                    e.Candidature.Candidat.Id == userId ||
                    e.Candidature.Candidat.Utilisateur.Id == userId)
                .OrderByDescending(e => e.DateScheduled ?? e.CreeLe)
                .ToListAsync();

            return entretiens.Select(e => (object)new
            {
                e.Id,
                e.Statut,
                e.DateScheduled,
                e.LienToken,
                e.Score,
                e.Transcript,
                e.CreeLe,
                TitreOffre    = e.Candidature.Offre.Titre,
                NomEntreprise = e.Candidature.Offre.Entreprise?.Nom ?? "RecruitSaaS",
                CandidatureId = e.CandidatureId
            }).ToList();
        }

        // ── Sauvegarder le transcript (réponses du candidat en temps réel) ──────
        public async Task SaveTranscriptAsync(Guid id, string transcript)
        {
            var entretien = await _db.EntretiensIA.FindAsync(id)
                ?? throw new Exception("Entretien introuvable");
            entretien.Transcript = transcript;
            entretien.MisAJourLe = DateTime.UtcNow;
            await _db.SaveChangesAsync();
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

            if (entretien == null || entretien.Statut == "Cancelled") return null;

            // Le lien est actif 5 minutes avant et pendant l'entretien (30 min)
            var now = DateTime.UtcNow;
            bool estActif = entretien.DateScheduled.HasValue
                && now >= entretien.DateScheduled.Value.AddMinutes(-5)
                && now <= entretien.DateScheduled.Value.AddMinutes(35);

            if (estActif && entretien.Statut == "Confirmed")
            {
                entretien.Statut    = "In Progress";
                entretien.LienActif = true;
                entretien.MisAJourLe = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // URL de la salle d'entretien (avatar 3D + média candidat)
            var frontendUrl  = _config["Frontend:Url"] ?? "http://localhost:5173";
            var avatarUrl    = $"{frontendUrl}/entretien-ia?token={Uri.EscapeDataString(token)}";

            var rawAvatar = await _db.CandidateProfiles
                .AsNoTracking()
                .Where(p => p.CandidatId == entretien.Candidature.CandidatId)
                .Select(p => p.AvatarUrl)
                .FirstOrDefaultAsync();

            var photoProfilUrl = ResolvePublicAssetUrl(rawAvatar);

            return new EntretienRejoindreResult
            {
                EstActif        = estActif,
                DateScheduled   = entretien.DateScheduled,
                LienEntretien   = estActif ? avatarUrl : null,
                NomCandidat     = entretien.Candidature.Candidat.Utilisateur.Nom,
                TitreOffre      = entretien.Candidature.Offre.Titre,
                EntretienId     = entretien.Id,
                Langue          = entretien.Langue ?? "FR",
                PhotoProfilUrl  = photoProfilUrl,
                QuestionsJson   = entretien.QuestionsJson
            };
        }

        /// <summary>Construit une URL exploitable par le navigateur (canvas + crossOrigin).</summary>
        private string? ResolvePublicAssetUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim();
            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return raw;

            var origin = _config["AppSettings:PublicOrigin"]?.TrimEnd('/')
                         ?? "http://localhost:5202";
            return raw.StartsWith('/') ? $"{origin}{raw}" : $"{origin}/{raw}";
        }

        public async Task<Dictionary<DateTime, int>> GetSlotUsageAsync(DateTime start, DateTime end)
        {
            var usage = await _db.EntretiensIA
                .Where(e => e.DateScheduled != null 
                            && e.DateScheduled >= start 
                            && e.DateScheduled <= end
                            && e.Statut != "Cancelled" 
                            && e.Statut != "Abandoned")
                .GroupBy(e => e.DateScheduled!.Value)
                .Select(g => new { Slot = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Slot, x => x.Count);

            // Ensure keys are treated as UTC for JSON serialization (adding 'Z')
            return usage.ToDictionary(
                k => DateTime.SpecifyKind(k.Key, DateTimeKind.Utc),
                v => v.Value
            );
        }

        // ── SESSION CONTROL ──────────────────────────────────────────────────────
        public async Task<object> GetSessionAsync(string token)
        {
            var entretien = await _db.EntretiensIA
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Offre)
                .Include(e => e.Candidature)
                    .ThenInclude(c => c.Candidat)
                    .ThenInclude(ca => ca.Utilisateur)
                .FirstOrDefaultAsync(e => e.LienToken == token);

            if (entretien == null)
                throw new Exception("Entretien introuvable ou lien invalide.");

            var now = DateTime.UtcNow;

            if (entretien.Statut == "Completed" || entretien.Statut == "Cancelled" || entretien.Statut == "Expired")
            {
                throw new Exception($"Session cannot be joined. Status: {entretien.Statut}");
            }

            // Check grace period if previously active
            if (entretien.Statut == "In Progress" || entretien.Statut == "Abandoned")
            {
                if (entretien.LastActivityAt.HasValue && (now - entretien.LastActivityAt.Value).TotalMinutes > 5)
                {
                    entretien.Statut = "Abandoned";
                    await _db.SaveChangesAsync();
                    throw new Exception("Grace period expired. Session abandoned.");
                }
            }

            // First time joining — we used to mark as In Progress here, 
            // but now we wait for an explicit StartSession call from the Gate.
            if (entretien.Statut == "Pending Confirmation")
            {
                // Just upgrade to Confirmed if it was pending, but don't start yet
                entretien.Statut = "Confirmed";
                await _db.SaveChangesAsync();
            }

            // Calculate remaining time
            int remainingSeconds = entretien.DurationMinutes * 60;
            if (entretien.StartedAt.HasValue)
            {
                remainingSeconds = Math.Max(0, entretien.DurationMinutes * 60 - (int)(now - entretien.StartedAt.Value).TotalSeconds);
            }

            // If time is up, mark as expired
            if (remainingSeconds <= 0)
            {
                entretien.Statut = "Expired";
                await _db.SaveChangesAsync();
                throw new Exception("Session time expired.");
            }

            return new
            {
                id = entretien.Id,
                status = entretien.Statut,
                currentQuestionIndex = entretien.CurrentQuestionIndex,
                transcript = entretien.Transcript,
                questionsJson = entretien.QuestionsJson,
                langue = entretien.Langue,
                remainingTimeSeconds = remainingSeconds,
                startedAt = entretien.StartedAt,
                dateScheduled = entretien.DateScheduled,
                durationMinutes = entretien.DurationMinutes,
                titreOffre = entretien.Candidature?.Offre?.Titre
            };
        }

        public async Task StartSessionAsync(string token)
        {
            var entretien = await _db.EntretiensIA.FirstOrDefaultAsync(e => e.LienToken == token);
            if (entretien == null) throw new Exception("Entretien introuvable.");

            if (entretien.Statut == "Completed" || entretien.Statut == "Cancelled")
                throw new Exception("Cannot start a finished or cancelled session.");

            var now = DateTime.UtcNow;
            
            // Only set startedAt if not already set (to prevent resetting if they rejoin)
            if (!entretien.StartedAt.HasValue)
            {
                entretien.StartedAt = now;
            }
            
            entretien.Statut = "In Progress";
            entretien.LastActivityAt = now;
            await _db.SaveChangesAsync();
        }

        public async Task PingSessionAsync(string token, int questionIndex, string transcript)
        {
            var entretien = await _db.EntretiensIA.FirstOrDefaultAsync(e => e.LienToken == token);
            if (entretien == null) throw new Exception("Entretien introuvable.");

            if (entretien.Statut != "In Progress") return;

            // Only allow ping if within grace period
            if (entretien.LastActivityAt.HasValue && (DateTime.UtcNow - entretien.LastActivityAt.Value).TotalMinutes > 5)
            {
                entretien.Statut = "Abandoned";
                await _db.SaveChangesAsync();
                throw new Exception("Session abandoned due to inactivity.");
            }

            entretien.CurrentQuestionIndex = questionIndex;
            
            // Only update transcript if not empty to prevent accidental clears
            if (!string.IsNullOrEmpty(transcript))
            {
                 // Si c'est un ping régulier qui envoie tout le texte, on remplace
                 entretien.Transcript = transcript;
            }

            entretien.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task CompleteSessionAsync(string token)
        {
            var entretien = await _db.EntretiensIA.FirstOrDefaultAsync(e => e.LienToken == token);
            if (entretien == null) throw new Exception("Entretien introuvable.");

            entretien.Statut = "Completed";
            entretien.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}