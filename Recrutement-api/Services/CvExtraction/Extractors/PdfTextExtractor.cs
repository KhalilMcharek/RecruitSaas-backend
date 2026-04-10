using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public class PdfTextExtractor : ITextExtractor
    {
        private readonly OcrExtractor _ocrExtractor;
        
        public PdfTextExtractor(OcrExtractor ocrExtractor)
        {
            _ocrExtractor = ocrExtractor;
        }

        public async Task<string> ExtractTextFromFileAsync(string filePath)
        {
            var textBuilder = new StringBuilder();
            bool hasText = false;

            using (PdfDocument document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    string pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        hasText = true;
                        textBuilder.AppendLine(pageText);
                    }
                }
            }

            string result = textBuilder.ToString().Trim();

            if (!hasText || result.Length < 50)
            {
                // Si le document ne contient quasiment que des images ou < 50 caractères
                // return await _ocrExtractor.ExtractTextFromFileAsync(filePath); 
                // Note : Pour l'instant on skip l'OCR sur PDF direct car il faudrait le convertir en image. 
                // Un OCR direct sur PDF requiert son extraction page par page en image.
                return result; 
            }

            return result;
        }
    }
}