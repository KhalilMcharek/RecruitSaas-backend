using System.Threading.Tasks;

namespace Recrutement_api.Services.CvExtraction
{
    public interface ICvParserService
    {
        Task<string> ExtractTextAsync(string fileUrl);
    }
}
