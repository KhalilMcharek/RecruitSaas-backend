using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recrutement_api.Models
{
    public class CandidateProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // FK vers Candidat (qui lui-même a FK vers Utilisateur)
        public Guid CandidatId { get; set; }
        [ForeignKey("CandidatId")]
        public Candidat Candidat { get; set; } = null!;

        // Infos personnelles
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public string? Bio { get; set; }
        public string? Seeking { get; set; }

        // Formation
        public string? Education { get; set; }
        public string? FieldOfStudy { get; set; }

        // Expérience
        public string? Experience { get; set; }
        public string? Availability { get; set; }

        // Skills stockées en JSON (ex: ["React","Python"])
        public string? SkillsJson { get; set; }

        // Liens
        public string? Linkedin { get; set; }
        public string? Github { get; set; }
        public string? PortfolioUrl { get; set; }

        // Image uploadée sur Cloudinary
        public string? AvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}