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
            _dbContext           = dbContext;
            _cvExtractionService = cvExtractionService;
            _httpClient          = httpClient;
            _logger              = logger;
        }

        // Helpers 

        private async Task<AnalyseCV> EnsureTextExtractedAsync(Guid candidatureId)
        {
            var candidature = await _dbContext.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.AnalyseCV)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
                throw new Exception("Candidature introuvable");

            if (candidature.AnalyseCV == null || string.IsNullOrWhiteSpace(candidature.AnalyseCV.TexteExtrait))
            {
                _logger.LogInformation($"Texte absent pour {candidatureId}. Extraction en cours...");
                return await _cvExtractionService.ProcessCvAsync(candidatureId);
            }

            return candidature.AnalyseCV;
        }

        private async Task<T> PostToAiAsync<T>(string endpoint, object payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content     = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_aiBaseUrl}/{endpoint}", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var options        = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(responseString, options);
        }

        //  Score de matching technique (TICKET-026) 
        // Mesure la pertinence des compétences techniques — indépendant du niveau séniorité
        public async Task<object> CalculateScoreAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre   = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre   = offre?.Titre,
                description  = offre?.Description,
                typeContrat  = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiScoreResponse>("score", payload);

            analyse.Score = aiResult.Score;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { score = analyse.Score };
        }

        //  Classification (TICKET-024) 
        // Qualified / Not Qualified / Over Qualified
        // INDÉPENDANT du score :
        // Score        = pertinence des compétences techniques (0-100)
        //  Classification = adéquation du niveau séniorité au poste
        //    Over Qualified : candidat trop senior (ex: senior 5ans pour poste junior)
        //    Not Qualified  : manque de compétences ou d'expérience
        //    Qualified      : niveau correspond au poste
        public async Task<object> ClassifyCandidateAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);
            var offre   = (await _dbContext.Candidatures
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == candidatureId))?.Offre;

            var payload = new
            {
                texteExtrait = analyse.TexteExtrait,
                titreOffre   = offre?.Titre,
                description  = offre?.Description,
                typeContrat  = offre?.TypeContrat.ToString()
            };

            var aiResult = await PostToAiAsync<AiClassifyResponse>("classify", payload);

            // Sauvegarde sans toucher au score
            analyse.Classification = aiResult.Classification;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { classification = analyse.Classification };
        }

        //  Résumé automatique (TICKET-025) 
        public async Task<object> SummarizeCvAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload  = new { texteExtrait = analyse.TexteExtrait };
            var aiResult = await PostToAiAsync<AiSummaryResponse>("summarize", payload);

            analyse.Resume = aiResult.Summary;
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { summary = analyse.Resume };
        }

        // Extraction de compétences (TICKET-023)
        public async Task<object> ExtractCompetencesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload  = new { texteExtrait = analyse.TexteExtrait };
            var aiResult = await PostToAiAsync<AiSkillsResponse>("extract-skills", payload);

            analyse.Competences = JsonSerializer.Serialize(aiResult.Skills);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { skills = aiResult.Skills };
        }

        //  Extraction d'expérience (TICKET-023) 
        public async Task<object> ExtractExperienceAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload  = new { texteExtrait = analyse.TexteExtrait };
            var aiResult = await PostToAiAsync<AiExperienceResponse>("extract-experience", payload);

            analyse.Experience = JsonSerializer.Serialize(aiResult.Experiences);
            _dbContext.AnalysesCV.Update(analyse);
            await _dbContext.SaveChangesAsync();

            return new { experiences = aiResult.Experiences };
        }

        // Extraction des entreprises citées (TICKET-023)
        public async Task<object> ExtractCompaniesAsync(Guid candidatureId)
        {
            var analyse = await EnsureTextExtractedAsync(candidatureId);

            var payload  = new { texteExtrait = analyse.TexteExtrait };
            var aiResult = await PostToAiAsync<AiCompaniesResponse>("extract-companies", payload);

            return new { companies = aiResult.Companies };
        }

        // DTOs de réponse 

        private class AiScoreResponse
        {
            public float Score { get; set; }
        }

        private class AiClassifyResponse
        {
            // "Qualified" | "Not Qualified" | "Over Qualified"
            public string Classification { get; set; } = string.Empty;
        }

        private class AiSummaryResponse
        {
            public string Summary { get; set; } = string.Empty;
        }

        private class AiSkillsResponse
        {
            public string[] Skills { get; set; } = Array.Empty<string>();
        }

        private class AiExperienceResponse
        {
            public object[] Experiences { get; set; } = Array.Empty<object>();
        }

        private class AiCompaniesResponse
        {
            public object[] Companies { get; set; } = Array.Empty<object>();
        }
    }
}