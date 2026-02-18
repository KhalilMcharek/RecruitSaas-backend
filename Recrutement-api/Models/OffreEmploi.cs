using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class OffreEmploi
    {
        [Key]
        public Guid Id { get; set; }

        public Guid EntrepriseId { get; set; }
        public Entreprise Entreprise { get; set; }

        [Required]
        public string Titre { get; set; }

        public string Description { get; set; }
        public string Localisation { get; set; }
        public bool EstPublie { get; set; } = false;
        public string LienPublic { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public FormulaireCandidature Formulaire { get; set; }
        public ICollection<Candidature> Candidatures { get; set; }
        public ICollection<AssignationExpert> Assignations { get; set; }
    }
}
