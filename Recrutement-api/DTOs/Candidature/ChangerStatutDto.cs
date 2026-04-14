namespace Recrutement_api.DTOs.Candidature
{
    public class ChangerStatutDto
    {
        public string Statut { get; set; } = "";
        // Valeurs acceptées : "Nouvelle" | "En cours" | "Acceptée" | "Refusée"
    }
}