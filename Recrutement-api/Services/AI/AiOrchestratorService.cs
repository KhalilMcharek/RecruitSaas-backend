using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        private readonly string _aiBaseUrl = "http://127.0.0.1:8000/ai";

        public AiOrchestratorService(
            ApplicationDbContext dbContext,
            ICvExtractionService cvExtractionService,
            HttpClient httpClient,
            ILogger<AiOrchestratorService> logger)
        {
            _dbContext           = dbContext;
            _cvExtractionService = cvExtractionService;
            _httpClient          = httpClient;
            _logger              = logger;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

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

        // ── Score (TICKET-026) ─────────────────────────────────────────────────
        public async Task<object> CalculateScoreAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
            {
                _logger.LogWarning($"[AI] Score : texte vide pour {candidatureId}");
                return new { score = 0, warning = "PDF illisible ou scanné" };
            }

            var offre = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload  = new { texteExtrait = analyse.TexteExtrait, titreOffre = offre?.Titre, description = offre?.Description, typeContrat = offre?.TypeContrat.ToString() };
            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            // Arrondi à l'entier pour éviter les micro-variations
            analyse.Score = (float)Math.Round(aiResult?.Score ?? 0);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { score = analyse.Score };
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
        public async Task<object> SummarizeCvAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            if (!HasUsableText(analyse))
                return new { summary = "", warning = "PDF illisible ou scanné" };

            var aiResult = await PostToAiAsync<AiSummaryResponse>("summarize", new { texteExtrait = analyse.TexteExtrait });

            analyse.Resume = aiResult?.Summary ?? "";
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { summary = analyse.Resume };
        }

        // ── Compétences (TICKET-023) ───────────────────────────────────────────
        public async Task<object> ExtractCompetencesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

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
        private class AiScoreResponse      { public float  Score          { get; set; } }
        private class AiClassifyResponse   { public string Classification { get; set; } = ""; }
        private class AiSummaryResponse    { public string Summary        { get; set; } = ""; }
        private class AiSkillsResponse     { public string[] Skills       { get; set; } = Array.Empty<string>(); }
        private class AiExperienceResponse { public object[] Experiences  { get; set; } = Array.Empty<object>(); }
        private class AiCompaniesResponse  { public object[] Companies    { get; set; } = Array.Empty<object>(); }
        private class AiCertificationsResponse { public object[] Certifications { get; set; } = Array.Empty<object>(); }
    }
}