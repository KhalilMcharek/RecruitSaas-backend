using System.ComponentModel.DataAnnotations;
using Recrutement_api.DTOs;

namespace Recrutement_api.Models
{
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; }

        public TenantStatus Statut { get; set; } = TenantStatus.Pending; 
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public ICollection<Entreprise> Entreprises { get; set; }
    }
}
