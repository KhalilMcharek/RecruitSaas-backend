using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    /// <summary>
    /// OCR extractor backed by PaddleOCR with locally-bundled models.
    /// The heavy PaddleOcrAll engine is initialised once (lazy singleton) and
    /// reused for every extraction call, so no model download or re-init
    /// overhead occurs after the first use.
    ///
    /// Supported languages: English + French (and all Latin-script languages).
    /// LocalFullModels.EnglishV3 covers accented characters (é, à, ê, ü …)
    /// which makes it suitable for French CVs without a dedicated French model.
    /// </summary>
    public class OcrExtractor : ITextExtractor, IDisposable
    {
        // ---------------------------------------------------------------------------
        // Singleton engine – created once per application lifetime.
        // Lazy<T> with LazyThreadSafetyMode.ExecutionAndPublication guarantees that
        // even under concurrent requests the engine is initialised exactly once.
        // ---------------------------------------------------------------------------
        private static readonly Lazy<PaddleOcrAll> _engineLazy =
            new Lazy<PaddleOcrAll>(CreateEngine, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly ILogger<OcrExtractor> _logger;

        public OcrExtractor(ILogger<OcrExtractor> logger)
        {
            _logger = logger;
        }

        // ---------------------------------------------------------------------------
        // Factory – called exactly once thanks to Lazy<T>
        // ---------------------------------------------------------------------------
        private static PaddleOcrAll CreateEngine()
        {
            // LocalFullModels.EnglishV3 is bundled inside the NuGet package
            // Sdcb.PaddleOCR.Models.Local – no network download required.
            // It supports Latin scripts including French accented characters.
            FullOcrModel model = LocalFullModels.EnglishV3;

            return new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,   // handles slightly rotated scans
                Enable180Classification = true // crucial for photos/scans that might be upside down
            };
        }

        // ---------------------------------------------------------------------------
        // Main extraction method
        // ---------------------------------------------------------------------------
        public Task<string> ExtractTextFromFileAsync(string filePath)
        {
            // Run synchronous OpenCV + PaddleOCR work on a thread-pool thread
            // so we don't block the ASP.NET request thread.
            return Task.Run(() => RunOcr(filePath));
        }

        private string RunOcr(string filePath)
        {
            var resultText = new StringBuilder();

            try
            {
                _logger.LogInformation("OCR: starting extraction for file {FilePath}", filePath);

                using var mat = Cv2.ImRead(filePath, ImreadModes.Color);

                if (mat.Empty())
                {
                    _logger.LogWarning("OCR: could not load image from {FilePath}", filePath);
                    throw new InvalidOperationException($"Impossible de charger l'image: {filePath}");
                }

                // Reuse the singleton engine – no lock needed: PaddleOcrAll.Run is
                // NOT thread-safe, so wrap in a lock for concurrent requests.
                PaddleOcrResult result;
                lock (_engineLazy.Value)
                {
                    result = _engineLazy.Value.Run(mat);
                }

                foreach (var region in result.Regions)
                {
                    if (!string.IsNullOrWhiteSpace(region.Text))
                    {
                        resultText.AppendLine(region.Text.Trim());
                    }
                }

                _logger.LogInformation(
                    "OCR: extraction complete – {CharCount} characters extracted from {FilePath}",
                    resultText.Length, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR: error while extracting text from {FilePath}", filePath);
                // Re-throw so the caller (CvExtractionService) can handle / log it.
                throw;
            }

            return resultText.ToString();
        }

        // ---------------------------------------------------------------------------
        // IDisposable – the static engine lives for the app lifetime, but expose
        // Dispose() so that it can be cleaned up during graceful shutdown if
        // OcrExtractor is registered as Singleton in the DI container.
        // ---------------------------------------------------------------------------
        private static bool _disposed;

        public void Dispose()
        {
            if (!_disposed && _engineLazy.IsValueCreated)
            {
                _engineLazy.Value.Dispose();
                _disposed = true;
            }
        }
    }
}