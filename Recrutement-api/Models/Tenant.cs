using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; }


        public string Statut { get; set; } // Actif, Suspendu, Inactif
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public ICollection<Entreprise> Entreprises { get; set; }
    }
}
