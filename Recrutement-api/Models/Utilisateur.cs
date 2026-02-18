using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public enum RoleUtilisateur
    {
        Admin,
        Tenant,
        Expert,
        Candidat
    }
    public class Utilisateur
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string MotDePasse { get; set; }

        public string Prenom { get; set; }
        public string Nom { get; set; }

        [Required]
        public RoleUtilisateur Role { get; set; }

        public bool EstActif { get; set; } = true;
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
    }
}
