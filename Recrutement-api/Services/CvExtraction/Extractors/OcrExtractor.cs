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
    public class OcrExtractor : ITextExtractor, IDisposable
    {
        private static readonly Lazy<PaddleOcrAll> _engineLazy =
            new Lazy<PaddleOcrAll>(CreateEngine, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly SemaphoreSlim _ocrSemaphore = new SemaphoreSlim(1, 1);

        private readonly ILogger<OcrExtractor> _logger;

        public OcrExtractor(ILogger<OcrExtractor> logger)
        {
            _logger = logger;
        }

        private static PaddleOcrAll CreateEngine()
        {
            FullOcrModel model = LocalFullModels.EnglishV3;
            return new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection    = true,
                Enable180Classification = true
            };
        }

        public Task<string> ExtractTextFromFileAsync(string filePath)
        {
            return Task.Run(() => RunOcr(filePath));
        }

        private string RunOcr(string filePath)
        {
            var resultText = new StringBuilder();

            try
            {
                _logger.LogInformation("OCR: starting extraction for {FilePath}", filePath);

                using var original = Cv2.ImRead(filePath, ImreadModes.Color);

                if (original.Empty())
                {
                    _logger.LogWarning("OCR: cannot load image from {FilePath}", filePath);
                    throw new InvalidOperationException($"Cannot load image: {filePath}");
                }

                // ── Prétraitement pour améliorer la qualité OCR ──────────────
                using var processed = PreprocessImage(original);

                // Essaie d'abord avec l'image prétraitée
                string text = RunOcrOnMat(processed);

                // Si résultat pauvre (< 50 chars ou ratio charabia élevé), essaie l'original
                if (string.IsNullOrWhiteSpace(text) || text.Length < 50 || IsGarbled(text))
                {
                    _logger.LogWarning("OCR: preprocessed result poor ({Len} chars), trying original", text.Length);
                    text = RunOcrOnMat(original);
                }

                // Si encore du charabia, essaie avec rotation 180°
                if (IsGarbled(text))
                {
                    _logger.LogWarning("OCR: result still garbled, trying 180° rotation");
                    using var rotated = RotateMat(original, 180);
                    string rotText = RunOcrOnMat(rotated);
                    if (!IsGarbled(rotText) && rotText.Length > text.Length)
                        text = rotText;
                }

                _logger.LogInformation("OCR: {Chars} characters extracted from {FilePath}", text.Length, filePath);
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR: error for {FilePath}", filePath);
                throw;
            }
        }

        private string RunOcrOnMat(Mat mat)
        {
            var sb = new StringBuilder();

            _ocrSemaphore.Wait();
            try
            {
                var result = _engineLazy.Value.Run(mat);
                foreach (var region in result.Regions)
                {
                    if (!string.IsNullOrWhiteSpace(region.Text))
                        sb.AppendLine(region.Text.Trim());
                }
            }
            finally
            {
                _ocrSemaphore.Release();
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Prétraitement : redimensionne si trop grand, améliore contraste.
        /// </summary>
        private Mat PreprocessImage(Mat src)
        {
            var result = new Mat();

            // Convertit en niveaux de gris pour améliorer la détection
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Amélioration du contraste (CLAHE)
            using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new Size(8, 8));
            using var enhanced = new Mat();
            clahe.Apply(gray, enhanced);

            // Reconvertit en BGR pour PaddleOCR
            Cv2.CvtColor(enhanced, result, ColorConversionCodes.GRAY2BGR);

            return result;
        }

        /// <summary>
        /// Détecte si le texte OCR est du charabia (trop de caractères non-alphabétiques).
        /// </summary>
        private bool IsGarbled(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 20)
                return true;

            int alphaCount = 0;
            int totalCount = 0;

            foreach (char c in text)
            {
                if (char.IsLetter(c) || char.IsDigit(c) || char.IsWhiteSpace(c))
                    alphaCount++;
                if (!char.IsWhiteSpace(c))
                    totalCount++;
            }

            if (totalCount == 0) return true;

            double alphaRatio = (double)alphaCount / (text.Length);

            // Si moins de 60% de caractères lisibles → charabia
            if (alphaRatio < 0.60) return true;

            // Vérifie si les mots ont une longueur raisonnable
            var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;

            double avgWordLen = 0;
            foreach (var w in words) avgWordLen += w.Length;
            avgWordLen /= words.Length;

            // Mots trop courts en moyenne = charabia (ex: "a b c d e")
            if (avgWordLen < 2.5 && words.Length > 10) return true;

            return false;
        }

        private Mat RotateMat(Mat src, double angle)
        {
            var center = new Point2f(src.Width / 2f, src.Height / 2f);
            using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var rotated = new Mat();
            Cv2.WarpAffine(src, rotated, rotMat, new Size(src.Width, src.Height));
            return rotated;
        }

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