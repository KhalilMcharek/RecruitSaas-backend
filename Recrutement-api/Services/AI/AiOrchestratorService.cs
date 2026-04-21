using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services.CvExtraction;

namespace Recrutement_api.Services.AI
{
    public class AiOrchestratorService : IAiOrchestratorService
    {
        // Verrou par candidature pour éviter les race conditions lors de l'extraction
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim>
            _extractionLocks = new();

        private readonly ApplicationDbContext _dbContext;
        private readonly ICvExtractionService _cvExtractionService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiOrchestratorService> _logger;
        private readonly NotificationService _notificationService; // ← AJOUTÉ
        private readonly string _aiBaseUrl = "http://127.0.0.1:8000/ai";

        public AiOrchestratorService(
            ApplicationDbContext dbContext,
            ICvExtractionService cvExtractionService,
            HttpClient httpClient,
            ILogger<AiOrchestratorService> logger,
            NotificationService notificationService) // ← AJOUTÉ
        {
            _dbContext            = dbContext;
            _cvExtractionService  = cvExtractionService;
            _httpClient           = httpClient;
            _logger               = logger;
            _notificationService  = notificationService; // ← AJOUTÉ
        }

        // ── Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Retourne l'AnalyseCV avec le texte extrait.
        /// - Si AnalyseCV null ou TexteExtrait null → extrait une SEULE fois
        /// - Si TexteExtrait = " " (sentinelle) → PDF déjà tenté, vide → ne re-tente pas
        /// </summary>
        private async Task<AnalyseCV> EnsureTextExtractedAsync(Guid candidatureId)
        {
            var candidature = await _dbContext.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.AnalyseCV)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
                throw new Exception("Candidature introuvable");

            // Texte déjà extrait et utilisable → retourne directement
            if (candidature.AnalyseCV != null
                && candidature.AnalyseCV.TexteExtrait != null
                && candidature.AnalyseCV.TexteExtrait != "EMPTY_PDF")
                return candidature.AnalyseCV;

            // Verrou par candidature : une seule extraction à la fois
            // Évite DbUpdateConcurrencyException quand plusieurs boutons sont cliqués en parallèle
            var semaphore = _extractionLocks.GetOrAdd(candidatureId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // Re-vérifie après acquisition du verrou (double-check)
                _dbContext.ChangeTracker.Clear();
                var fresh = await _dbContext.Candidatures
                    .Include(c => c.AnalyseCV)
                    .FirstOrDefaultAsync(c => c.Id == candidatureId);

                if (fresh?.AnalyseCV != null
                    && fresh.AnalyseCV.TexteExtrait != null
                    && fresh.AnalyseCV.TexteExtrait != "EMPTY_PDF")
                    return fresh.AnalyseCV;

                _logger.LogInformation($"Texte absent pour {candidatureId}. Extraction en cours...");
                return await _cvExtractionService.ProcessCvAsync(candidatureId);
            }
            finally
            {
                semaphore.Release();
                // Nettoie le verrou après usage
                _extractionLocks.TryRemove(candidatureId, out _);
            }
        }

        /// <summary>
        /// Retourne true si le texte est utilisable par l'IA (longueur suffisante).
        /// </summary>
        private static bool HasUsableText(AnalyseCV analyse)
            => !string.IsNullOrWhiteSpace(analyse.TexteExtrait)
               && analyse.TexteExtrait.Trim().Length >= 20;

        /// <summary>
        /// Appelle le microservice Python.
        /// Gère 400 (texte vide) et autres erreurs sans crasher.
        /// </summary>
        private async Task<T?> PostToAiAsync<T>(string endpoint, object payload)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{_aiBaseUrl}/{endpoint}", content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AI] Impossible de joindre {endpoint} : {ex.Message}");
                return default;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var detail = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"[AI] {endpoint} → 400 (PDF vide/scanné) : {detail}");
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                _logger.LogError($"[AI] {endpoint} → {(int)response.StatusCode} : {detail}");
                return default;
            }

            var json    = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(json, options);
        }

        // ── Score + changement de statut automatique ─────────────────
        // Score < 30   → "Refusée"
        // Score 30–80  → reste "Nouvelle" (pending)
        // Score > 80   → "En cours" (In Review)
        // ── Score (TICKET-026) ─────────────────────────────────────────────────
        public async Task<object> CalculateScoreAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            // Charger la candidature avec Offre ET Candidat pour la notification
            var candidature = await _dbContext.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            var offre = candidature?.Offre;

            var payload  = new { texteExtrait = analyse.TexteExtrait, titreOffre = offre?.Titre, description = offre?.Description, typeContrat = offre?.TypeContrat.ToString() };
            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            // Arrondi à l'entier pour éviter les micro-variations
            analyse.Score = (float)Math.Round(aiResult?.Score ?? 0);
            _dbContext.AnalysesCV.Update(analyse);

            // 2 — ★ Déterminer le nouveau statut selon le score
            string? nouveauStatut = aiResult.Score switch
            {
                < 30  => "Refusée",
                > 80  => "En cours",
                _     => null   // 30–80 : on ne change pas le statut
            };

            // 3 — Appliquer le statut si nécessaire et notifier
            if (nouveauStatut != null && candidature != null)
            {
                var ancienStatut = candidature.Statut;

                if (ancienStatut != nouveauStatut)
                {
                    candidature.Statut = nouveauStatut;

                    // ★ Notifier le candidat (même pattern que ChangerStatutAsync)
                    if (candidature.Candidat != null)
                    {
                        await _notificationService.NotifyStatutChangeAsync(
                            candidatId:    candidature.Candidat.Id,
                            candidatureId: candidature.Id,
                            offreTitre:    offre?.Titre ?? "Unknown Position",
                            nouveauStatut: nouveauStatut,
                            offreId:       offre?.Id
                        );
                    }

                    _logger.LogInformation(
                        $"[AI Score] Candidature {candidatureId} → score={aiResult.Score} → statut={nouveauStatut}");
                }
            }

            await _dbContext.SaveChangesAsync();

            return new
            {
                score        = analyse.Score,
                domainFit   = aiResult?.DomainFit,
                technical   = aiResult?.Technical,
                experience  = aiResult?.Experience,
                statutAuto   = nouveauStatut ?? "unchanged",
                message      = nouveauStatut switch
                {
                    "Refusée"  => "Score too low — application automatically rejected.",
                    "En cours" => "Excellent score — application moved to In Review.",
                    _          => "Score saved — application remains pending."
                }};
            // Sous-scores : renvoyés au client uniquement (non persistés — pas de champs sur AnalyseCV).
           
        }

        // ── Score en lecture seule (retourne le score sauvegardé sans recalculer) ──
        public async Task<object> GetScoreAsync(Guid candidatureId)
        {
            var analyse = await _dbContext.AnalysesCV
                .FirstOrDefaultAsync(a => a.CandidatureId == candidatureId);

            if (analyse?.Score != null)
                return new { score = analyse.Score };

            // Pas encore calculé → calcule et sauvegarde
            return await CalculateScoreAsync(candidatureId);
        }

        // ── Classification (TICKET-024) ────────────────────────────────────────
        public async Task<object> ClassifyCandidateAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
            {
                _logger.LogWarning($"[AI] Classify : texte vide pour {candidatureId}");
                return new { classification = "Not Qualified", warning = "PDF illisible ou scanné" };
            }

            var offre = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload  = new { texteExtrait = analyse.TexteExtrait, titreOffre = offre?.Titre, description = offre?.Description, typeContrat = offre?.TypeContrat.ToString() };
            var aiResult = await PostToAiAsync<AiClassifyResponse>("classify", payload);

            analyse.Classification = aiResult?.Classification ?? "Not Qualified";
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { classification = analyse.Classification };
        }

        // ── Résumé (TICKET-025) ────────────────────────────────────────────────
        // Le résumé est contextualisé : on envoie aussi le titre + la description
        // de l'offre pour que l'IA croise le profil avec les exigences du poste.
        public async Task<object> SummarizeCvAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
                return new { summary = "", warning = "PDF illisible ou scanné" };

            var offre = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre   = offre?.Titre,
                description  = offre?.Description,
                typeContrat  = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiSummaryResponse>("summarize", payload);

            analyse.Resume = aiResult?.Summary ?? "";
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { summary = analyse.Resume };
        }

        // ── Compétences (TICKET-023) ───────────────────────────────────────────
        public async Task<object> ExtractCompetencesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var offre = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload  = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre   = offre?.Titre,
                description  = offre?.Description
            };
            if (!HasUsableText(analyse))
                return new { skills = Array.Empty<string>(), warning = "PDF illisible ou scanné" };

            var aiResult = await PostToAiAsync<AiSkillsResponse>("extract-skills", new { texteExtrait = analyse.TexteExtrait });

            var skills = aiResult?.Skills ?? Array.Empty<string>();
            analyse.Competences = JsonSerializer.Serialize(skills);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { skills };
        }

        // ── Expérience (TICKET-023) ────────────────────────────────────────────
        public async Task<object> ExtractExperienceAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
                return new { experiences = Array.Empty<object>(), warning = "PDF illisible ou scanné" };

            var aiResult = await PostToAiAsync<AiExperienceResponse>("extract-experience", new { texteExtrait = analyse.TexteExtrait });

            var experiences = aiResult?.Experiences ?? Array.Empty<object>();
            analyse.Experience = JsonSerializer.Serialize(experiences);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { experiences };
        }

        // ── Entreprises (TICKET-023) ───────────────────────────────────────────
        public async Task<object> ExtractCompaniesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
                return new { companies = Array.Empty<object>(), warning = "PDF illisible ou scanné" };

            var aiResult = await PostToAiAsync<AiCompaniesResponse>("extract-companies", new { texteExtrait = analyse.TexteExtrait });

            return new { companies = aiResult?.Companies ?? Array.Empty<object>() };
        }

        // ── Certifications (TICKET-023) ───────────────────────────────────────────
        public async Task<object> ExtractCertificationsAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
                return new { certifications = Array.Empty<object>(), warning = "PDF illisible ou scanné" };

            var aiResult = await PostToAiAsync<AiCertificationsResponse>("extract-certifications", new { texteExtrait = analyse.TexteExtrait });

            var certifications = aiResult?.Certifications ?? Array.Empty<object>();

            // Sauvegarde en base comme Competences et Experience
            analyse.Certifications = JsonSerializer.Serialize(certifications);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { certifications };
        }

                // ── DTOs ───────────────────────────────────────────────────────────────
        private class AiScoreResponse
        {
            public float Score { get; set; }

            [JsonPropertyName("domain_fit")]
            public float? DomainFit { get; set; }

            [JsonPropertyName("technical")]
            public float? Technical { get; set; }

            [JsonPropertyName("experience")]
            public float? Experience { get; set; }
        }
        private class AiClassifyResponse   { public string Classification { get; set; } = ""; }
        private class AiSummaryResponse    { public string Summary        { get; set; } = ""; }
        private class AiSkillsResponse     { public string[] Skills        { get; set; } = Array.Empty<string>(); }
        private class AiExperienceResponse { public object[] Experiences  { get; set; } = Array.Empty<object>(); }
        private class AiCompaniesResponse  { public object[] Companies    { get; set; } = Array.Empty<object>(); }
        private class AiCertificationsResponse { public object[] Certifications { get; set; } = Array.Empty<object>(); }
    }
}