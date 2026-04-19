// Models/CandidatNotification.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recrutement_api.Models
{
    [Table("CandidatNotifications")]
    public class CandidatNotification
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid CandidatId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Type { get; set; }  // "application_accepted", "interview_scheduled", etc.
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Body { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        [Required]
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        
        public DateTime? LuLe { get; set; }
        
        // Relations optionnelles
        public Guid? OffreId { get; set; }
        public Guid? CandidatureId { get; set; }
            
    // ★ Ajouts pour quiz
    public string? QuizToken { get; set; }
    public string? Metadata { get; set; }  // JSON pour données supplémentaires
        
        // Navigation properties (optionnel)
        // public Candidat Candidat { get; set; }
        // public OffreEmploi Offre { get; set; }
        // public Candidature Candidature { get; set; }
    }
}