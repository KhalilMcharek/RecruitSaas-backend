namespace Recrutement_api.DTOs.Entretien
{
    public class PlanifierDto
    {
        public List<DateTime> Creneaux { get; set; } = new();
        public string? Message { get; set; }
    }

    public class ConfirmerCreneauDto
    {
        public DateTime DateChoisie { get; set; }
    }
}