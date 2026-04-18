using System.ComponentModel.DataAnnotations;
namespace Recrutement_api.Models
{
    public class AnalyseCV
    {
        [Key]
        public Guid Id { get; set; }
        public Guid CandidatureId { get; set; }
        public Candidature Candidature { get; set; }
        public string TexteExtrait { get; set; } = string.Empty;
        public string? Competences { get; set; } = string.Empty;
        public string? Experience { get; set; } = string.Empty;
        public string? Certifications { get; set; } = string.Empty;  // NOUVEAU
        public string? Classification { get; set; } = string.Empty;
        public float? Score { get; set; }
        public string? Resume { get; set; } = string.Empty;
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
    }
}