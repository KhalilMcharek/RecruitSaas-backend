using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Recrutement_api.Models;
public enum ExpertRole   { Evaluator, SeniorEvaluator, Manager }
public enum ExpertStatus { Pending, Active, Inactive }
public class Expert
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string Email     { get; set; } = string.Empty;
    public bool   IsActive  { get; set; } = false;
    public bool   IsInvited { get; set; } = true;
    public string? TemporaryPassword { get; set; }
    public string? Poste    { get; set; }
    public string? Phone { get; set; }
    public DateTime CreeLe    { get; set; } = DateTime.UtcNow;
    public ExpertRole   Role      { get; set; } = ExpertRole.Evaluator;
    public ExpertStatus Status    { get; set; } = ExpertStatus.Pending;
    public DateTime     UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CompanyId { get; set; }
    [ForeignKey("CompanyId")]
    public Entreprise Company { get; set; } = null!;
    public Guid   TenantId { get; set; }
    public Tenant Tenant   { get; set; } = null!;
    public Guid?  UtilisateurId { get; set; }
    public Guid?  EntrepriseId  { get; set; }
    public Utilisateur? Utilisateur { get; set; }
    public Entreprise?  Entreprise  { get; set; }
    public ICollection<AssignationExpert> Assignations { get; set; } = new List<AssignationExpert>();
    public ICollection<AvisExpert> Avis { get; set; } = new List<AvisExpert>();
}