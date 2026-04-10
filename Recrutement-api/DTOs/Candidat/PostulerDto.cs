using Microsoft.AspNetCore.Http;

namespace Recrutement_api.DTOs.Candidat
{
    /// <summary>
    /// Input DTO for the Postuler endpoint — groups the multipart form fields.
    /// </summary>
    public class PostulerDto
    {
        public Guid OffreId { get; set; }
        public IFormFile Cv { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PortfolioUrl { get; set; }
        public string? Motivation { get; set; }
        /// <summary>JSON-encoded dictionary of custom form field responses.</summary>
        public string? ChampsPersonnalises { get; set; }
    }

    /// <summary>
    /// Response returned when a candidature is successfully created.
    /// </summary>
    public class PostulerResultDto
    {
        public string Message { get; set; } = string.Empty;
        public Guid CandidatureId { get; set; }
        public string CvUrl { get; set; } = string.Empty;
    }
}
