using System;
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class EntretienIA
    {
        [Key] // obligatoire pour EF Core
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CandidatureId { get; set; }
        public Candidature? Candidature { get; set; }

        public DateTime DateScheduled { get; set; }
        public string? Transcript { get; set; }
        public float? Score { get; set; }
        public string? Statut { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
    }
}
