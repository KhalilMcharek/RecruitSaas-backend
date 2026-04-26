using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services.CvExtraction.Processors;
using Recrutement_api.Services.CvExtraction.Extractors;
using Microsoft.Extensions.Logging;

namespace Recrutement_api.Services.CvExtraction
{
    public interface ICvExtractionService
    {
        Task<AnalyseCV> ProcessCvAsync(Guid candidatureId);
    }

    public class CvExtractionService : ICvExtractionService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CvExtractionService> _logger;
        private readonly PdfTextExtractor _pdfExtractor;
        private readonly DocxTextExtractor _docxExtractor;
        private readonly HttpClient _httpClient;

        public CvExtractionService(
            ApplicationDbContext dbContext,
            ILogger<CvExtractionService> logger,
            PdfTextExtractor pdfExtractor,
            DocxTextExtractor docxExtractor,
            HttpClient httpClient)
        {
            _dbContext           = dbContext;
            _logger              = logger;
            _pdfExtractor        = pdfExtractor;
            _docxExtractor       = docxExtractor;
            _httpClient          = httpClient;
        }

        public async Task<AnalyseCV> ProcessCvAsync(Guid candidatureId)
        {
            _logger.LogInformation($"Début d'extraction du CV pour la candidature {candidatureId}");

            var candidature = await _dbContext.Candidatures
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null || string.IsNullOrEmpty(candidature.CvUrl))
            {
                _logger.LogWarning($"Candidature introuvable ou CvUrl manquant : {candidatureId}");
                throw new Exception("CV introuvable pour cette candidature.");
            }

            var analyse = await _dbContext.AnalysesCV
                .FirstOrDefaultAsync(a => a.CandidatureId == candidatureId);

            if (analyse == null)
            {
                analyse = new AnalyseCV
                {
                    Id            = Guid.NewGuid(),
                    CandidatureId = candidatureId,
                    CreeLe        = DateTime.UtcNow
                };
                _dbContext.AnalysesCV.Add(analyse);
            }

            string cleanedText = "";
            try
            {
                string rawText = await ExtractTextAsync(candidature.CvUrl);
                cleanedText    = TextCleaner.Clean(rawText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Extraction échouée pour {candidatureId}");
                cleanedText = "";
            }

            // Sentinelle : espace = "déjà tenté, texte vide"
            // Évite la boucle infinie dans EnsureTextExtractedAsync
            analyse.TexteExtrait = string.IsNullOrWhiteSpace(cleanedText) ? "EMPTY_PDF" : cleanedText;

            // Upsert manuel pour éviter DbUpdateConcurrencyException
            // quand plusieurs requêtes parallèles tentent de sauvegarder le même AnalyseCV
            try
            {
                var existing = await _dbContext.AnalysesCV
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.CandidatureId == candidatureId);

                if (existing == null)
                    _dbContext.AnalysesCV.Add(analyse);
                else
                    _dbContext.AnalysesCV.Update(analyse);

                await _dbContext.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                // Une autre requête parallèle a déjà sauvegardé — on recharge depuis la DB
                _logger.LogWarning($"Concurrency conflict pour {candidatureId} — rechargement depuis la DB");
                _dbContext.ChangeTracker.Clear();
                var fresh = await _dbContext.AnalysesCV
                    .FirstOrDefaultAsync(a => a.CandidatureId == candidatureId);
                if (fresh != null)
                {
                    fresh.TexteExtrait = analyse.TexteExtrait;
                    await _dbContext.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                $"Extraction terminée pour {candidatureId}. " +
                $"Texte : {cleanedText.Length} caractères.");

            return analyse;
        }

        internal async Task<string> ExtractTextAsync(string fileUrl)
        {
            string tempFilePath = Path.GetTempFileName();

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(fileUrl);

                string urlLower = fileUrl.ToLower();
                string ext = urlLower.Contains(".docx") || urlLower.Contains(".doc") ? ".docx" : ".pdf";

                tempFilePath = Path.ChangeExtension(tempFilePath, ext);
                await File.WriteAllBytesAsync(tempFilePath, fileBytes);

                if (ext == ".docx")
                    return await _docxExtractor.ExtractTextFromFileAsync(tempFilePath);

                // ── PDF ──────────────────────────────────────────────────────
                return await _pdfExtractor.ExtractTextFromFileAsync(tempFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur extraction : {fileUrl}");
                throw;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    try { File.Delete(tempFilePath); } catch { }
            }
        }

    }
}
