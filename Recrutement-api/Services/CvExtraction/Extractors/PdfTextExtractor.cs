using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public class PdfTextExtractor : ITextExtractor
    {
        public PdfTextExtractor(OcrExtractor ocrExtractor) { }

        public async Task<string> ExtractTextFromFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var textBuilder = new StringBuilder();

                // UseLenientParsing ignore les erreurs de structure PDF
                var options = new ParsingOptions { UseLenientParsing = true };

                try
                {
                    using var document = PdfDocument.Open(filePath, options);
                    int pageCount = document.NumberOfPages;

                    // Itère page par page avec un numéro explicite
                    // pour que l'exception d'une page n'arrête pas les autres
                    for (int i = 1; i <= pageCount; i++)
                    {
                        try
                        {
                            var page = document.GetPage(i);
                            string pageText = page.Text;
                            if (!string.IsNullOrWhiteSpace(pageText))
                                textBuilder.AppendLine(pageText);
                        }
                        catch (Exception pageEx)
                        {
                            // Police corrompue sur cette page  on skippe
                            Console.WriteLine($"[WARN] Page {i} skippée : {pageEx.Message}");
                        }
                    }
                }
                catch (Exception docEx)
                {
                    // Le document lui-même est illisible
                    Console.WriteLine($"[WARN] PDF illisible, texte vide retourné : {docEx.Message}");
                }

                return textBuilder.ToString().Trim();
            });
        }
    }
}