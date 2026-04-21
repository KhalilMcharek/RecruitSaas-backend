using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recrutement_api.Models
{
    public class SavedJob
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CandidatId { get; set; }  // Guid, not int

    [Required]
    public Guid OffreId { get; set; }     // Guid, not int

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CandidatId))]
    public Candidat Candidat { get; set; } = null!;

    [ForeignKey(nameof(OffreId))]
    public OffreEmploi Offre { get; set; } = null!;
}
}