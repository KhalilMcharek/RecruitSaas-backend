using System.Text.RegularExpressions;

namespace Recrutement_api.Services.CvExtraction.Processors
{
    public class TextCleaner
    {
        public static string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            // Enlever les espaces multiples
            string cleaned = Regex.Replace(input, @"\s+", " ");
            
            // Corriger les césures (ex: "mo- teur" -> "moteur")
            cleaned = Regex.Replace(cleaned, @"(\w+)-\s+(\w+)", "$1$2");

            // Enlever les caractères non imprimables
            cleaned = Regex.Replace(cleaned, @"[^\u0009\u000A\u000D\u0020-\u007E\u0085\u00A0-\uD7FF\uE000-\uFDCF\uFDE0-\uFFFD]", string.Empty);

            return cleaned.Trim();
        }
    }
}
