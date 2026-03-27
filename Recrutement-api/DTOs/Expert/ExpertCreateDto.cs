namespace Recrutement_api.DTOs.Expert
{
    public class ExpertCreateDto
    {
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MotDePasse { get; set; } = string.Empty;
        public Guid EntrepriseId { get; set; }
        public string Poste { get; set; } = string.Empty;
    }
}
