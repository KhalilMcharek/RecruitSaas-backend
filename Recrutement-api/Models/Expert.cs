using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class Expert
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; }

        public Guid EntrepriseId { get; set; }
        public Entreprise Entreprise { get; set; }

        public string Poste { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public ICollection<AssignationExpert> Assignations { get; set; }
        public ICollection<AvisExpert> Avis { get; set; }
    }
}
