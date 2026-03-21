using Recrutement_api.Models;

namespace Recrutement_api.DTOs.Offre
{
    public class ChampPersonnaliseDto
    {
        public Guid? Id { get; set; }
        public string Nom { get; set; }


        public string Question { get; set; }

        public TypeChamp Type { get; set; }

        public bool EstObligatoire { get; set; }

        public string? OptionsJson { get; set; }

        public int Ordre { get; set; }
    }
}
