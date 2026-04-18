using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    /// <summary>
    /// Extrait le texte d'une image via Tesseract OCR.
    /// Supporte le français et l'anglais automatiquement.
    /// </summary>
    public class TesseractTextExtractor : IDisposable
    {
        private readonly ILogger<TesseractTextExtractor> _logger;
        private const string TesseractDataPath = @"C:\Program Files\Tesseract-OCR\tessdata";
        private const string Languages = "fra+eng"; // Français + Anglais

        public TesseractTextExtractor(ILogger<TesseractTextExtractor> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextFromImageAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("[Tesseract] Extraction depuis {Path}", imagePath);

                    using var engine = new TesseractEngine(TesseractDataPath, Languages, EngineMode.Default);
                    using var img = Pix.LoadFromFile(imagePath);
                    using var page = engine.Process(img);

                    string text = page.GetText()?.Trim() ?? "";
                    float confidence = page.GetMeanConfidence();

                    _logger.LogInformation(
                        "[Tesseract] {Chars} caractères extraits, confiance: {Conf:P0}",
                        text.Length, confidence);

                    return text;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Tesseract] Erreur extraction {Path}", imagePath);
                    throw;
                }
            });
        }

        public void Dispose() { }
    }
}