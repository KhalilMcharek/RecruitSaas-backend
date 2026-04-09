using System.Threading.Tasks;

namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public interface ITextExtractor
    {
        Task<string> ExtractTextFromFileAsync(string filePath);
    }
}
