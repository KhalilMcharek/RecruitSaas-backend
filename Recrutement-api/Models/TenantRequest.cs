using System;
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class TenantRequest
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string CompanyName { get; set; }   // Nom de l'entreprise

        [Required]
        public string RNE { get; set; }           // Numéro RNE

        [Required]
        public string Owner { get; set; }         // Nom du propriétaire

        [Required]
        public string WorkEmail { get; set; }     // Email de contact

        [Required]
        public string Password { get; set; }      // Mot de passe (hashé à l'avenir)

        [Required]
        public string Industry { get; set; }      // Secteur d’activité

        public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}