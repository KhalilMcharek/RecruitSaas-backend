using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recrutement_api.Models
{
    public class TenantProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // FK vers Tenant (qui lui-même a FK vers Utilisateur)
        public Guid TenantId { get; set; }
        [ForeignKey("TenantId")]
        public Tenant Tenant { get; set; } = null!;

        // Infos recruteur
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }

        // Présence en ligne
        public string? Website { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }

        // Préférences de recrutement
        public string? HiringStatus { get; set; } // "actively" | "sometimes" | "paused"

        // Listes stockées en JSON (ex: ["Full-time","Remote"])
        public string? WorkTypesJson { get; set; }
        public string? TechStackJson { get; set; }

        // Logo uploadé sur Cloudinary
        public string? LogoUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}