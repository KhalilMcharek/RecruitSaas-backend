using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class AssignationExpert
    {
        [Key]
        public Guid Id { get; set; }

        public Guid OffreId { get; set; }
        public OffreEmploi Offre { get; set; }

        public Guid ExpertId { get; set; }
        public Expert Expert { get; set; }

        public DateTime AssigneLe { get; set; } = DateTime.UtcNow;
    }
}
