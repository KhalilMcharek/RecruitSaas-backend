using Recrutement_api.Models;

namespace Recrutement_api.DTOs.Offre
{
    public class OffreResponseDto
    {
        public Guid Id { get; set; }

        public string Titre { get; set; }

        public TypeContrat TypeContrat { get; set; }

        public string Description { get; set; }

        public string Localisation { get; set; }

        public bool EstPublie { get; set; }

        public string LienPublic { get; set; }

        public DateTime CreeLe { get; set; }

        // Infos minimales entreprise
        public Guid EntrepriseId { get; set; }
        public string NomEntreprise { get; set; }

        public FormulaireResponseDto? Formulaire { get; set; }
    }
}
