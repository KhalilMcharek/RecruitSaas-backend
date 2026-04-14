using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public class PdfTextExtractor : ITextExtractor
    {
       
        
        public PdfTextExtractor(OcrExtractor ocrExtractor)
        {
           
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

         

            return result;
        }
    }
}