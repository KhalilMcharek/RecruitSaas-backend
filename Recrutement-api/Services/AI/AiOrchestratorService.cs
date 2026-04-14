using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services.CvExtraction;

namespace Recrutement_api.Services.AI
{
    public class AiOrchestratorService : IAiOrchestratorService
    {
        /// <summary>
        /// Secours dev uniquement si Groq:ApiKey et GROQ_API_KEY sont vides.
        /// À supprimer ou externaliser avant production (risque de fuite dans le dépôt).
        /// </summary>
        private const string DevGroqApiKeyFallback =
            "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";

        private readonly ApplicationDbContext _dbContext;
        private readonly ICvExtractionService _cvExtractionService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiOrchestratorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _aiBaseUrl = "http://127.0.0.1:8000/ai";

        public AiOrchestratorService(
            ApplicationDbContext dbContext,
            ICvExtractionService cvExtractionService,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AiOrchestratorService> logger)
        {
            _dbContext = dbContext;
            _cvExtractionService = cvExtractionService;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        private async Task<AnalyseCV> EnsureTextExtractedAsync(Guid candidatureId)
        {
            var candidature = await _dbContext.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.AnalyseCV)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
            {
                throw new Exception("Candidature introuvable");
            }

            if (candidature.AnalyseCV == null || string.IsNullOrWhiteSpace(candidature.AnalyseCV.TexteExtrait))
            {
                _logger.LogInformation($"Text not found for candidature {candidatureId}. Extracting CV text...");
                return await _cvExtractionService.ProcessCvAsync(candidatureId);
            }

            return candidature.AnalyseCV;
        }

        private async Task<T> PostToAiAsync<T>(string endpoint, object payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{_aiBaseUrl}/{endpoint}", content);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "IA {Endpoint} : impossible de contacter le service ({Base})", endpoint, _aiBaseUrl);
                throw new AiUpstreamException(
                    (int)HttpStatusCode.ServiceUnavailable,
                    ex.Message,
                    "Le service IA (Python) est injoignable. Vérifiez qu'il tourne sur l'URL configurée.");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                var logBody = responseString.Length > 2000 ? responseString[..2000] : responseString;
                _logger.LogWarning("IA POST {Endpoint} → HTTP {Code}: {Body}", endpoint, code, logBody);
                throw new AiUpstreamException(
                    code,
                    responseString,
                    $"Erreur du service IA sur « {endpoint} » (HTTP {code}).");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(responseString, options)
                   ?? throw new InvalidOperationException($"Réponse IA vide ou invalide pour « {endpoint} ».");
        }

        public async Task<object> CalculateScoreAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre = (await _dbContext.Candidatures.Include(c => c.Offre).FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre = offre?.Titre,
                description = offre?.Description,
                typeContrat = offre?.TypeContrat.ToString(),
                groqApiKey = ResolveGroqApiKey()
            };

            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            var technical = aiResult.Technical;
            var experience = aiResult.Experience;
            var domainFit = aiResult.DomainFit;

            analyse.Score = aiResult.Score;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new
            {
                score = analyse.Score,
                technical,
                experience,
                domainFit
            };
        }

        public async Task<object> ProfileFromCvAsync(Guid candidatureId)
        {
            // Toujours réextraire depuis l’URL CV actuelle : un nouveau fichier peut remplacer l’ancien
            // alors que TexteExtrait en base restait celui du CV précédent.
            var candidature = await _dbContext.Candidatures
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == candidatureId);
            if (candidature == null)
                throw new Exception("Candidature introuvable");
            if (string.IsNullOrWhiteSpace(candidature.CvUrl))
                throw new Exception("Aucun CV joint à cette candidature.");

            var analyse = await _cvExtractionService.ProcessCvAsync(candidatureId);
            if (string.IsNullOrWhiteSpace(analyse.TexteExtrait))
                throw new Exception("Texte du CV vide après extraction.");

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                groqApiKey = ResolveGroqApiKey()
            };
            var aiResult = await PostToAiAsync<AiProfilClassifyResponse>("profile", payload);
            return new
            {
                domain = aiResult.Domain ?? "Autre",
                experienceLevel = aiResult.ExperienceLevel ?? "Confirmé",
                jobType = aiResult.JobType ?? "Autre"
            };
        }

        public async Task<object> ClassifyCandidateAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre = (await _dbContext.Candidatures.Include(c => c.Offre).FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                groqApiKey = ResolveGroqApiKey(),
                titreOffre = offre?.Titre,
                description = offre?.Description,
                typeContrat = offre?.TypeContrat.ToString(),
                score = analyse.Score
            };

            var aiResult = await PostToAiAsync<AiMatchClassifyResponse>("classify", payload);
            var label = string.IsNullOrWhiteSpace(aiResult.Classification) ? "Not Qualified" : aiResult.Classification.Trim();
            if (label != "Qualified" && label != "Not Qualified" && label != "Over Qualified")
                label = "Not Qualified";
            analyse.Classification = label;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new
            {
                classification = analyse.Classification,
                reason = string.IsNullOrWhiteSpace(aiResult.Reason) ? null : aiResult.Reason.Trim(),
                candidateJobType = string.IsNullOrWhiteSpace(aiResult.CandidateJobType) ? null : aiResult.CandidateJobType.Trim()
            };
        }

        public async Task<object> SummarizeCvAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre = (await _dbContext.Candidatures.Include(c => c.Offre).FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            object payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                groqApiKey = ResolveGroqApiKey(),
                titreOffre = offre?.Titre,
                description = offre?.Description,
                typeContrat = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiSummaryResponse>("summarize", payload);

            analyse.Resume = aiResult.Summary;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { summary = analyse.Resume };
        }

        public async Task<object> ExtractCompetencesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait
            };

            var aiResult = await PostToAiAsync<AiSkillsResponse>("extract-skills", payload);

            // Storing as JSON string or joining based on how AnalyseCV.Competences is typed (it's string?)
            analyse.Competences = JsonSerializer.Serialize(aiResult.Skills);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { skills = aiResult.Skills };
        }

        public async Task<object> ExtractExperienceAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait
            };

            var aiResult = await PostToAiAsync<AiExperienceResponse>("extract-experience", payload);

            analyse.Experience = JsonSerializer.Serialize(aiResult.Experiences);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { experiences = aiResult.Experiences };
        }

        private string ResolveGroqApiKey()
        {
            var groqKey = _configuration["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(groqKey))
                groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(groqKey))
                groqKey = DevGroqApiKeyFallback;
            return groqKey;
        }

        // Response DTOs
        private class AiScoreResponse
        {
            public float Score { get; set; }
            public float Technical { get; set; }
            public float Experience { get; set; }
            [JsonPropertyName("domainFit")]
            public float DomainFit { get; set; }
        }
        private class AiMatchClassifyResponse
        {
            public string Classification { get; set; } = "";
            public string? Reason { get; set; }
            [JsonPropertyName("candidateJobType")]
            public string? CandidateJobType { get; set; }
        }

        private class AiProfilClassifyResponse
        {
            public string Domain { get; set; } = "";
            [JsonPropertyName("experienceLevel")]
            public string ExperienceLevel { get; set; } = "";
            [JsonPropertyName("jobType")]
            public string JobType { get; set; } = "";
        }
        private class AiSummaryResponse { public string Summary { get; set; } }
        private class AiSkillsResponse { public string[] Skills { get; set; } }
        private class AiExperienceResponse { public object[] Experiences { get; set; } }
    }
}
