using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class Candidature
    {
        [Key]
        public Guid Id { get; set; }

        public Guid OffreId { get; set; }
        public OffreEmploi Offre { get; set; }

        public Guid CandidatId { get; set; }
        public Candidat Candidat { get; set; }

        public string CvUrl { get; set; }

        /// <summary>
        /// Pipeline status: Nouvelle | En cours | Présélectionné | Entretien | Acceptée | Refusée
        /// </summary>
        public string Statut { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public string FormulaireResponses { get; set; }
        public AnalyseCV AnalyseCV { get; set; }

        // public ICollection<EntretienIA> Entretiens { get; set; }
        public ICollection<AvisExpert> Avis { get; set; }

        // ── Automation fields (set by AI score rules) ──────────────────────

        /// <summary>
        /// Set to true when automatically promoted to Présélectionné (score >= 80).
        /// Acts as a UI hint to surface the quiz generation modal.
        /// </summary>
        public bool? EnableQuiz { get; set; }

        /// <summary>
        /// Priority hint set by score automation.
        /// Null = normal, "LOW" = score below 50.
        /// </summary>
        public string? Priority { get; set; }

        /// <summary>
        /// JSON array of automation flags, e.g. ["LOW_SCORE"].
        /// Stored as a serialized string for simplicity.
        /// </summary>
        public string? Flags { get; set; }
    }
}
