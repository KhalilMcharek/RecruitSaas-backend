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
        private readonly OcrExtractor _ocrExtractor;
        private readonly HttpClient _httpClient;

        public CvExtractionService(
            ApplicationDbContext dbContext,
            ILogger<CvExtractionService> logger,
            PdfTextExtractor pdfExtractor,
            DocxTextExtractor docxExtractor,
            OcrExtractor ocrExtractor,
            HttpClient httpClient)
        {
            _dbContext = dbContext;
            _logger = logger;
            _pdfExtractor = pdfExtractor;
            _docxExtractor = docxExtractor;
            _ocrExtractor = ocrExtractor;
            _httpClient = httpClient;
        }

        public async Task<AnalyseCV> ProcessCvAsync(Guid candidatureId)
        {
            _logger.LogInformation($"Début d'extraction du CV pour la candidature {candidatureId}");

            try
            {
                var candidature = await _dbContext.Candidatures.FirstOrDefaultAsync(c => c.Id == candidatureId);
                if (candidature == null || string.IsNullOrEmpty(candidature.CvUrl))
                {
                    _logger.LogWarning($"Candidature introuvable ou CvUrl manquant pour l'ID: {candidatureId}");
                    throw new Exception("CV introuvable pour cette candidature.");
                }

                string rawText = await ExtractTextAsync(candidature.CvUrl);
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
                
                return analyse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'extraction du CV pour la candidature {candidatureId}");
                throw;
            }
        }

        private async Task<string> ExtractTextAsync(string fileUrl)
        {
            string tempFilePath = Path.GetTempFileName();
            string textResult = "";

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(fileUrl);
                
                string ext = ".pdf";
                if (fileUrl.ToLower().Contains(".doc") || fileUrl.ToLower().Contains(".docx"))
                {
                    ext = ".docx";
                }
                else if (fileUrl.ToLower().Contains(".png") || fileUrl.ToLower().Contains(".jpg") || fileUrl.ToLower().Contains(".jpeg"))
                {
                    ext = ".jpg";
                }
                
                tempFilePath = tempFilePath + ext;
                await File.WriteAllBytesAsync(tempFilePath, fileBytes);

                if (ext == ".pdf")
                {
                    textResult = await _pdfExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
                else if (ext == ".docx")
                {
                    textResult = await _docxExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
                else
                {
                    // OCR
                    textResult = await _ocrExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors du téléchargement ou du parsing du fichier: {fileUrl}");
                throw;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }

            return textResult ?? "";
        }
    }
}
