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
using Recrutement_api.Services.StatusPromotion;
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
        private readonly CandidateStatusService _candidateStatusService;

        private readonly string _aiBaseUrl = "http://127.0.0.1:8000/ai";

        public AiOrchestratorService(
            ApplicationDbContext dbContext,
            ICvExtractionService cvExtractionService,
            HttpClient httpClient,
            ILogger<AiOrchestratorService> logger,
            CandidateStatusService candidateStatusService)
        {
            _dbContext            = dbContext;
            _cvExtractionService  = cvExtractionService;
            _httpClient           = httpClient;
            _logger               = logger;
            _candidateStatusService = candidateStatusService;
        }


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

    
        private static bool HasUsableText(AnalyseCV analyse)
            => !string.IsNullOrWhiteSpace(analyse.TexteExtrait)
               && analyse.TexteExtrait.Trim().Length >= 20;

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

            var offre = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload  = new { texteExtrait = analyse.TexteExtrait, titreOffre = offre?.Titre, description = offre?.Description, typeContrat = offre?.TypeContrat.ToString() };
            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            // Arrondi à l'entier pour éviter les micro-variations
            analyse.Score = (float)Math.Round(aiResult?.Score ?? 0);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            // ── Auto-promote status based on score (only from "Nouvelle") ──────
            var promotion = await _candidateStatusService.ApplyScoreRulesAsync(
                candidatureId, analyse.Score ?? 0f);

            return new
            {
                score      = analyse.Score,
                domainFit  = aiResult?.DomainFit,
                technical  = aiResult?.Technical,
                experience = aiResult?.Experience,
                // Status promotion info
                newStatus     = promotion.NewStatus,
                statusChanged = promotion.StatusChanged,
                enableQuiz    = promotion.EnableQuiz,
                flags         = promotion.Flags,
            };
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

        // ── Génération de Quiz ────────────────────────────────────────────────
        public async Task<object> GenerateQuizAsync(Guid offreId, int numQuestions = 10, int timePerQuestion = 60)
        {
            var offre = await _dbContext.OffresEmploi.FindAsync(offreId);
            if (offre == null)
                throw new Exception("Offre introuvable");

            var payload = new
            {
                titreOffre        = offre.Titre,
                description       = offre.Description,
                num_questions     = Math.Max(3, Math.Min(numQuestions, 20)),
                time_per_question = Math.Max(15, Math.Min(timePerQuestion, 300))
            };

            var aiResult = await PostToAiAsync<AiQuizResponse>("generate-quiz", payload);
            return aiResult ?? new AiQuizResponse();
        }

        // ── Entretien AI ──────────────────────────────────────────────────────
        public async Task<object> GenerateInterviewQuestionsAsync(Guid candidatureId, int numQuestions = 5, string langue = "FR")
        {
            var candidature = await _dbContext.Candidatures.Include(c => c.Offre).FirstOrDefaultAsync(c => c.Id == candidatureId);
            if (candidature == null || candidature.Offre == null) throw new Exception("Candidature ou Offre introuvable");

            var payload = new
            {
                titreOffre = candidature.Offre.Titre,
                description = candidature.Offre.Description,
                langue = langue,
                num_questions = numQuestions
            };
            return await PostToAiAsync<object>("generate-interview-questions", payload) ?? new { questions = Array.Empty<string>() };
        }

        public async Task<object> GenerateTtsAsync(string text, string langue = "FR")
        {
            var payload = new { text, langue };
            return await PostToAiAsync<object>("tts-elevenlabs", payload) ?? new { };
        }

        public async Task<object> GenerateSttAsync(System.IO.Stream audioStream, string filename)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(audioStream);
                // The API might need content type depending on the browser's recording
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
                content.Add(streamContent, "audio", filename);

                var response = await _httpClient.PostAsync($"{_aiBaseUrl}/stt-elevenlabs", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[AI] stt-elevenlabs → {(int)response.StatusCode} : {detail}");
                    return new { text = "", error = detail };
                }

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<object>(json, options) ?? new { text = "" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AI] Impossible de joindre stt-elevenlabs : {ex.Message}");
                return new { text = "", error = ex.Message };
            }
        }

        // ── ReplyInterviewAsync is disabled — simplified interview uses pre-generated questions ──
        // public async Task<object> ReplyInterviewAsync(Guid entretienId, string questionActuelle, string reponseCandidat, string langue = "FR")
        // { ... }

        public async Task<object> EvaluateInterviewAsync(Guid entretienId)
        {
            var entretien = await _dbContext.EntretiensIA
                .Include(e => e.Candidature).ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(e => e.Id == entretienId) ?? throw new Exception("Entretien introuvable");

            var payload = new
            {
                transcript = entretien.Transcript,
                titreOffre = entretien.Candidature.Offre.Titre,
                description = entretien.Candidature.Offre.Description,
                langue = entretien.Langue
            };

            var aiResult = await PostToAiAsync<AiEvaluateResponse>("evaluate-answers", payload);

            if (aiResult != null)
            {
                entretien.Score = aiResult.Score;
                entretien.Summary = aiResult.Summary;
                entretien.Strengths = JsonSerializer.Serialize(aiResult.Strengths);
                entretien.Weaknesses = JsonSerializer.Serialize(aiResult.Weaknesses);
                await _dbContext.SaveChangesAsync();
                return aiResult;
            }

            return new { score = 0, summary = "Erreur", strengths = Array.Empty<string>(), weaknesses = Array.Empty<string>() };
        }

        // ── DTOs ───────────────────────────────────────────────────────────────
        private class AiQuizQuestion
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("question")]
            public string Question { get; set; } = "";

            [JsonPropertyName("choices")]
            public string[] Choices { get; set; } = Array.Empty<string>();

            [JsonPropertyName("correct_index")]
            public int CorrectIndex { get; set; }
        }

        private class AiQuizResponse
        {
            [JsonPropertyName("questions")]
            public AiQuizQuestion[] Questions { get; set; } = Array.Empty<AiQuizQuestion>();
        }

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
        
        private class AiReplyResponse { public string Reply { get; set; } = ""; }
        
        private class AiEvaluateResponse
        {
            public float Score { get; set; }
            public string Summary { get; set; } = "";
            public string[] Strengths { get; set; } = Array.Empty<string>();
            public string[] Weaknesses { get; set; } = Array.Empty<string>();
        }
    }
}