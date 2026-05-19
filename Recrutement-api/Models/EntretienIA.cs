using System.ComponentModel.DataAnnotations;
namespace Recrutement_api.Models
{
    public class EntretienIA
    {
        [Key]
        public Guid Id { get; set; }
        public Guid CandidatureId { get; set; }
        public Candidature Candidature { get; set; }

        public string? CreneauxDisponibles { get; set; }

        public DateTime? DateScheduled { get; set; }

        public string? LienToken { get; set; }

        public bool LienActif { get; set; } = false;

        public Guid? PlanifiePar { get; set; }

        public string Transcript { get; set; } = string.Empty;
        public string? QuestionsJson { get; set; }
        public string Langue { get; set; } = "FR"; // Default to French
        public float? Score { get; set; }
        public string? Summary { get; set; }
        public string? Strengths { get; set; } // JSON array of strengths
        public string? Weaknesses { get; set; } // JSON array of weaknesses

        // Server-controlled session state
        public int CurrentQuestionIndex { get; set; } = 0;
        public DateTime? StartedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int DurationMinutes { get; set; } = 30;

        public string Statut { get; set; } = "Pending Confirmation";
        // Statuts : Pending Confirmation | Confirmed | In Progress | Completed | Abandoned | Expired | Cancelled

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public DateTime? MisAJourLe { get; set; }
    }
}