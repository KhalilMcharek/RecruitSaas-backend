using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{

    public enum TypeContrat
    {
        [Description("Contrat à Durée Indéterminée")]
        CDI,

        [Description("Contrat à Durée Déterminée")]
        CDD,

        [Description("Freelance / Indépendant")]
        Freelance,

        [Description("Stage")]
        Stage,

        [Description("Alternance")]
        Alternance,

        [Description("Intérim")]
        Interim
    }
    public class OffreEmploi
    {
        [Key]
        public Guid Id { get; set; }

        public Guid EntrepriseId { get; set; }
        public Entreprise Entreprise { get; set; }

        [Required]
        public string Titre { get; set; }
        public TypeContrat TypeContrat { get; set; }

        public string Description { get; set; }
        public string Localisation { get; set; }
        public bool EstPublie { get; set; } = false;
        public bool IsPublicLinkEnabled { get; set; } = false;

        // 🔹 Token sécurisé
        public string? PublicToken { get; set; }
        public DateTime? PublicLinkExpiresAt { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public FormulaireCandidature Formulaire { get; set; }
        public ICollection<Candidature> Candidatures { get; set; }
        public ICollection<AssignationExpert> Assignations { get; set; } = new List<AssignationExpert>();
    }
}
