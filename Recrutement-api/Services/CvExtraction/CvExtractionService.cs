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
using UglyToad.PdfPig;
using OpenCvSharp;
using System.Text;

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
        private readonly TesseractTextExtractor _tesseractExtractor;
        private readonly HttpClient _httpClient;

        public CvExtractionService(
            ApplicationDbContext dbContext,
            ILogger<CvExtractionService> logger,
            PdfTextExtractor pdfExtractor,
            DocxTextExtractor docxExtractor,
            OcrExtractor ocrExtractor,
            TesseractTextExtractor tesseractExtractor,
            HttpClient httpClient)
        {
            _dbContext           = dbContext;
            _logger              = logger;
            _pdfExtractor        = pdfExtractor;
            _docxExtractor       = docxExtractor;
            _ocrExtractor        = ocrExtractor;
            _tesseractExtractor  = tesseractExtractor;
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
                string ext = urlLower.Contains(".docx") || urlLower.Contains(".doc") ? ".docx"
                           : urlLower.Contains(".png") || urlLower.Contains(".jpg") || urlLower.Contains(".jpeg") ? ".jpg"
                           : ".pdf";

                tempFilePath = Path.ChangeExtension(tempFilePath, ext);
                await File.WriteAllBytesAsync(tempFilePath, fileBytes);

                if (ext == ".docx")
                    return await _docxExtractor.ExtractTextFromFileAsync(tempFilePath);

                if (ext == ".jpg")
                    return await _ocrExtractor.ExtractTextFromFileAsync(tempFilePath);

                // ── PDF ──────────────────────────────────────────────────────
                // Étape 1 : extraction texte PdfPig
                string text = await _pdfExtractor.ExtractTextFromFileAsync(tempFilePath);

                // Étape 2 : si vide → PDF scanné → OCR page par page
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogInformation($"PDF sans texte → OCR page par page : {fileUrl}");
                    text = await OcrPdfAsync(tempFilePath);
                }

                return text;
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

        /// <summary>
        /// Convertit chaque page PDF en image PNG puis applique PaddleOCR.
        /// Utilise PdfPig pour rasteriser (via GetPageImage si dispo)
        /// ou PDFiumSharp/Pdfium comme fallback.
        /// </summary>
        private async Task<string> OcrPdfAsync(string pdfPath)
        {
            var sb = new StringBuilder();

            try
            {
                // Tente la conversion avec PDFium (Pdfium.NET SDK ou docnet.core)
                // Si pas disponible, lève une exception catchée ci-dessous
                int pageCount = GetPdfPageCount(pdfPath);

                for (int i = 0; i < pageCount; i++)
                {
                    string pageImagePath = Path.ChangeExtension(
                        Path.GetTempFileName(), ".png");

                    try
                    {
                        bool converted = await RenderPdfPageToImageAsync(pdfPath, i, pageImagePath);
                        if (!converted || !File.Exists(pageImagePath)) continue;

                        // Tesseract en priorité (meilleur pour le français et les PDFs rotationnés)
                        string pageText;
                        try
                        {
                            pageText = await _tesseractExtractor.ExtractTextFromImageAsync(pageImagePath);
                        }
                        catch
                        {
                            // Fallback PaddleOCR si Tesseract échoue
                            pageText = await _ocrExtractor.ExtractTextFromFileAsync(pageImagePath);
                        }
                        if (!string.IsNullOrWhiteSpace(pageText))
                            sb.AppendLine(pageText);
                    }
                    finally
                    {
                        if (File.Exists(pageImagePath))
                            try { File.Delete(pageImagePath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"OCR PDF échoué : {ex.Message}");
            }

            string result = sb.ToString().Trim();
            _logger.LogInformation($"OCR PDF terminé : {result.Length} caractères extraits");
            return result;
        }

        private int GetPdfPageCount(string pdfPath)
        {
            try
            {
                var options = new ParsingOptions { UseLenientParsing = true };
                using var doc = PdfDocument.Open(pdfPath, options);
                return doc.NumberOfPages;
            }
            catch { return 1; }
        }

        /// <summary>
        /// Rasterise une page PDF en image PNG.
        /// Utilise Docnet.Core si disponible, sinon tente via un process pdftoppm/ghostscript.
        /// </summary>
        private async Task<bool> RenderPdfPageToImageAsync(string pdfPath, int pageIndex, string outputImagePath)
        {
            // ── Tentative 1 : Docnet.Core (Docnet.Core NuGet) ──────────────
            try
            {
                return await RenderWithDocnetAsync(pdfPath, pageIndex, outputImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Docnet.Core non disponible : {ex.Message}");
            }

            // ── Tentative 2 : pdftoppm (Poppler, souvent installé sur Linux) ──
            try
            {
                return await RenderWithPdftoppmAsync(pdfPath, pageIndex, outputImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"pdftoppm non disponible : {ex.Message}");
            }

            return false;
        }

        private async Task<bool> RenderWithDocnetAsync(string pdfPath, int pageIndex, string outputImagePath)
        {
            _logger.LogInformation($"[Docnet] Tentative rendu page {pageIndex} de {pdfPath}");

            using var lib        = Docnet.Core.DocLib.Instance;
            using var docReader  = lib.GetDocReader(pdfPath,
                new Docnet.Core.Models.PageDimensions(1200, 1600));
            using var pageReader = docReader.GetPageReader(pageIndex);

            int    w      = pageReader.GetPageWidth();
            int    h      = pageReader.GetPageHeight();
            byte[] pixels = pageReader.GetImage();

            _logger.LogInformation($"[Docnet] Page {pageIndex} rendue : {w}x{h}, {pixels.Length} bytes");

            using var mat = Mat.FromPixelData(h, w, MatType.CV_8UC4, pixels);
            Cv2.ImWrite(outputImagePath, mat);

            _logger.LogInformation($"[Docnet] Image sauvegardée : {outputImagePath}");
            await Task.CompletedTask;
            return File.Exists(outputImagePath);
        }

        private async Task<bool> RenderWithPdftoppmAsync(string pdfPath, int pageIndex, string outputImagePath)
        {
            int pageNum  = pageIndex + 1;
            string stem  = Path.GetTempFileName();
            string args  = $"-r 200 -f {pageNum} -l {pageNum} -png \"{pdfPath}\" \"{stem}\"";

            var psi = new System.Diagnostics.ProcessStartInfo("pdftoppm", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) throw new Exception("pdftoppm introuvable");
            await proc.WaitForExitAsync();

            // pdftoppm génère stem-00001.png
            string generated = $"{stem}-{pageNum:D5}.png";
            if (!File.Exists(generated))
                generated = Directory.GetFiles(Path.GetDirectoryName(stem)!,
                    Path.GetFileName(stem) + "*.png").FirstOrDefault() ?? "";

            if (!File.Exists(generated)) return false;
            File.Move(generated, outputImagePath, overwrite: true);
            return true;
        }
    }
}