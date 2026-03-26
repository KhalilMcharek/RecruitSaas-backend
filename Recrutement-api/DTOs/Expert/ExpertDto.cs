namespace Recrutement_api.DTOs.Expert
{
    public class ExpertDto
    {
        public Guid Id { get; set; }

   
        public Guid UtilisateurId { get; set; }
        public string Nom { get; set; }
        public Guid EntrepriseId { get; set; }
        public string? NomEntreprise { get; set; } 
        public string Poste { get; set; }
    }
}
