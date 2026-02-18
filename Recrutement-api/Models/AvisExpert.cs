using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class AvisExpert
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CandidatureId { get; set; }
        public Candidature Candidature { get; set; }

        public Guid ExpertId { get; set; }
        public Expert Expert { get; set; }

        public float Score { get; set; }
        public string Commentaire { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
    }
}
