namespace Recrutement_api.DTOs.Offre
{
    public class FormulaireResponseDto
    {
        public Guid Id { get; set; }
        public List<ChampPersonnaliseResponseDto> Champs { get; set; } = new List<ChampPersonnaliseResponseDto>();
    }
}
