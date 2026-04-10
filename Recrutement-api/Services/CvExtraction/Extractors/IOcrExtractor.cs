namespace Recrutement_api.Services.CvExtraction.Extractors
{
    public interface IOcrExtractor
    {
        Task<string> ExtractTextFromImageAsync(string filePath);
    }
}
