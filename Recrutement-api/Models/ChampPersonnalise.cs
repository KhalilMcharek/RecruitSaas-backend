using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public enum TypeChamp
    {
        Texte,
        Nombre,
        Date,
        ChoixMultiple,
        Fichier // Pour les pièces jointes requises spécifiques
    }
    public class ChampPersonnalise
    {
        [Key]
        public Guid Id { get; set; }

        public Guid FormulaireId { get; set; }
        public FormulaireCandidature Formulaire { get; set; }
        [Required]
        public string Nom { get; set; } // Identifiant interne du champ (ex: "annees_experience")

        [Required]
        public string Question { get; set; } // Label affiché au candidat

        public TypeChamp Type { get; set; }
        public bool EstObligatoire { get; set; }

        public string? OptionsJson { get; set; } // Pour stocker les valeurs possibles si ChoixMultiple (ex: "['Junior', 'Senior']")
        public int Ordre { get; set; } // L'ordre d'affichage dans le formulaire
    }
}
