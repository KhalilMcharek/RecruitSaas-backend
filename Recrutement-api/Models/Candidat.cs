using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class Candidat
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; }

        public string? Telephone { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public ICollection<Candidature> Candidatures { get; set; }
    }
}
