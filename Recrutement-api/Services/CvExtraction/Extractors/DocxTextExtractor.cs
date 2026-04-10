using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public class DocxTextExtractor : ITextExtractor
    {
        public Task<string> ExtractTextFromFileAsync(string filePath)
        {
            var textBuilder = new StringBuilder();

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var paragraph in body.Elements<Paragraph>())
                    {
                        textBuilder.AppendLine(paragraph.InnerText);
                    }
                }
            }

            return Task.FromResult(textBuilder.ToString().Trim());
        }
    }
}