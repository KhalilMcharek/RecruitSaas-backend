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
        public string Statut { get; set; } // Nouvelle, En cours, Acceptée, Refusée
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public string FormulaireResponses { get ; set ;}
        public AnalyseCV AnalyseCV { get; set; }
       // public ICollection<EntretienIA> Entretiens { get; set; }
        public ICollection<AvisExpert> Avis { get; set; }
    }

  
}
