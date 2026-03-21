using Recrutement_api.Models;

namespace Recrutement_api.DTOs.Offre
{
    public class OffreCreateDto
    {
        public Guid EntrepriseId { get; set; }
        public string Titre { get; set; }

        public string Description { get; set; }

        public string Localisation { get; set; }

        public TypeContrat TypeContrat { get; set; }

        public bool EstPublie { get; set; }

    }
}
