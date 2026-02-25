using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recrutement_api.Models
{
    public class EntretienIA
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CandidatureId { get; set; }

        [ForeignKey("CandidatureId")]
        public Candidature Candidature { get; set; }

        public DateTime DateEntretien { get; set; } = DateTime.UtcNow;

        public string? QuestionsPosees { get; set; }

        public string? ReponsesCandidat { get; set; }

        public double? ScoreIA { get; set; }

        public string? Evaluation { get; set; }
    }
}