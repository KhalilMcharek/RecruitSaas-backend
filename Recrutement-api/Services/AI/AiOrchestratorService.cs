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
            _dbContext = dbContext;
            _cvExtractionService = cvExtractionService;
            _httpClient = httpClient;
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

            var response = await _httpClient.PostAsync($"{_aiBaseUrl}/{endpoint}", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(responseString, options);
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
                typeContrat = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            analyse.Score = aiResult.Score;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { score = analyse.Score };
        }

        public async Task<object> ClassifyCandidateAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre = (await _dbContext.Candidatures.Include(c => c.Offre).FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre = offre?.Titre,
                description = offre?.Description,
                typeContrat = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiClassifyResponse>("classify", payload);

            analyse.Classification = aiResult.Classification;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { classification = analyse.Classification };
        }

        public async Task<object> SummarizeCvAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait
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

        // Response DTOs
        private class AiScoreResponse { public float Score { get; set; } }
        private class AiClassifyResponse { public string Classification { get; set; } }
        private class AiSummaryResponse { public string Summary { get; set; } }
        private class AiSkillsResponse { public string[] Skills { get; set; } }
        private class AiExperienceResponse { public object[] Experiences { get; set; } }
    }
}
