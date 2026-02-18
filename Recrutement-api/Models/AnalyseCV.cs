using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class AnalyseCV
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CandidatureId { get; set; }
        public Candidature Candidature { get; set; }

        public string TexteExtrait { get; set; }
        public string Competences { get; set; }
        public string Experience { get; set; }
        public string Classification { get; set; }
        public float? Score { get; set; }
        public string Resume { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

    }
}
