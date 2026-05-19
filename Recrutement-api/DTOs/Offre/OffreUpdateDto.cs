using Recrutement_api.Models;
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Offre
{
    public class OffreUpdateDto
    {
        
        public string? Titre { get; set; }

        public TypeContrat? TypeContrat { get; set; }

        public string? Description { get; set; }

        public string? Localisation { get; set; }

        public bool? EstPublie { get; set; }
        public DateTime? Deadline { get; set; }
        public int? MaxApplications { get; set; }
    }
}
