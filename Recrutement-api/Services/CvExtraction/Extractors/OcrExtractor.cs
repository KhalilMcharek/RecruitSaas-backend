using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleInference;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public class OcrExtractor : ITextExtractor
    {
        public Task<string> ExtractTextFromFileAsync(string filePath)
        {
            var resultText = new StringBuilder();

            try
            {
                using var ocr = new PaddleOcrAll(LocalFullModels.ChineseV4, PaddleDevice.Mkldnn());

                using var mat = Cv2.ImRead(filePath, ImreadModes.Color);
                var result = ocr.Run(mat);

                foreach (var region in result.Regions)
                {
                    resultText.AppendLine(region.Text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur OCR : {ex.Message}");
            }

            return Task.FromResult(resultText.ToString());
        }

        public bool CanHandle(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();

            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }
    }
}