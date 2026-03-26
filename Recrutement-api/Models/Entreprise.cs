using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.Models
{
    public class Entreprise
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; }

        [Required]
        public string Nom { get; set; }
        [Required]
        public string Secteur { get; set; }
        [Required]
        public string RNE { get; set; }

        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        public ICollection<Expert> Experts { get; set; }
        public ICollection<OffreEmploi> Offres { get; set; }
    }
}