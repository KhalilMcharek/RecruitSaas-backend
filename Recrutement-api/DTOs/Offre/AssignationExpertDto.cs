using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Offre
{
    public class AssignationExpertDto
    {
        [Required]
        public List<Guid> ExpertIds { get; set; }
        public Guid entrepriseId { get; set; }

    }
}
