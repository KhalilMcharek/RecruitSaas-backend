using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services.CvExtraction.Processors;
using Microsoft.Extensions.Logging;

namespace Recrutement_api.Services.CvExtraction
{
    public class CvBackgroundProcessingService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICvParserService _cvParserService;
        private readonly ILogger<CvBackgroundProcessingService> _logger;

        public CvBackgroundProcessingService(
            ApplicationDbContext dbContext,
            ICvParserService cvParserService,
            ILogger<CvBackgroundProcessingService> logger)
        {
            _dbContext = dbContext;
            _cvParserService = cvParserService;
            _logger = logger;
        }

        // Sera appelé par Hangfire
        public async Task ProcessCvTaskAsync(Guid candidatureId, string fileUrl)
        {
            _logger.LogInformation($"Début d'extraction du CV pour la candidature {candidatureId}");

            try
            {
                string rawText = await _cvParserService.ExtractTextAsync(fileUrl);
                string cleanedText = TextCleaner.Clean(rawText);

                // Rechercher ou créer l'AnalyseCV existante pour cette candidature
                var analyse = await _dbContext.AnalysesCV.FirstOrDefaultAsync(a => a.CandidatureId == candidatureId);
                
                if (analyse == null)
                {
                    analyse = new AnalyseCV
                    {
                        Id = Guid.NewGuid(),
                        CandidatureId = candidatureId,
                        TexteExtrait = cleanedText,
                        CreeLe = DateTime.UtcNow
                    };
                    _dbContext.AnalysesCV.Add(analyse);
                }
                else
                {
                    analyse.TexteExtrait = cleanedText;
                    _dbContext.AnalysesCV.Update(analyse);
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Extraction du CV terminée pour la candidature {candidatureId}. Texte extrait : {cleanedText.Length} caractères.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'extraction du CV pour la candidature {candidatureId}");
                throw; // Relancer pour que Hangfire sache que ça a échoué et le retente si besoin
            }
        }
    }
}
