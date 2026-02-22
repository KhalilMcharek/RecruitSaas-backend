using System;
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
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        // Hash du mot de passe
        [Required]
        public string MotDePasseHash { get; set; }

        public string Prenom { get; set; }
        public string Nom { get; set; }

        [Required]
        public RoleUtilisateur Role { get; set; }

        public bool EstActif { get; set; } = true;

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public Utilisateur() { }

        public Utilisateur(string email, string motDePasseHash, RoleUtilisateur role, string prenom = null, string nom = null)
        {
            Id = Guid.NewGuid();
            Email = email;
            MotDePasseHash = motDePasseHash;
            Role = role;
            Prenom = prenom;
            Nom = nom;
            EstActif = true;
            CreeLe = DateTime.UtcNow;
        }
    }
}
