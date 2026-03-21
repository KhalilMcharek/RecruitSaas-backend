using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class FormulaireCandidature
    {

        [Key]
        public Guid Id { get; set; }

        public Guid OffreId { get; set; }
        public OffreEmploi Offre { get; set; }
        public ICollection<ChampPersonnalise> ChampsPersonnalises { get; set; } = new List<ChampPersonnalise>();
    }
}
