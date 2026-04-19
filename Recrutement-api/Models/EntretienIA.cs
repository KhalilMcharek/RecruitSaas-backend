using System.ComponentModel.DataAnnotations;
namespace Recrutement_api.Models
{
    public class EntretienIA
    {
        [Key]
        public Guid Id { get; set; }
        public Guid CandidatureId { get; set; }
        public Candidature Candidature { get; set; }

        // Créneaux proposés par le tenant/expert (JSON array de DateTime)
        public string? CreneauxDisponibles { get; set; }

        // Créneau choisi par le candidat
        public DateTime? DateScheduled { get; set; }

        // Lien unique envoyé au candidat (token sécurisé)
        public string? LienToken { get; set; }

        // Lien devient actif uniquement à DateScheduled
        public bool LienActif { get; set; } = false;

        // Qui a planifié (tenant ou expert)
        public Guid? PlanifiePar { get; set; }

        public string Transcript { get; set; } = string.Empty;
        public float? Score { get; set; }
        public string Statut { get; set; } = "EnAttente";
        // Statuts : EnAttente | LienEnvoye | Planifie | EnCours | Termine | Annule

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public DateTime? MisAJourLe { get; set; }
    }
}