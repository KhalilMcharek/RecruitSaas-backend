using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Recrutement_api.Models;


namespace Recrutement_api.DTOs.Expert;
public class InviteExpertDto
{
    [Required(ErrorMessage = "Le prenom est obligatoire.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "Format email invalide.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'entreprise est obligatoire.")]
    public Guid CompanyId { get; set; }
    public string? Poste { get; set; }
}

public class UpdateExpertDto
{
    [Required(ErrorMessage = "Le prenom est obligatoire.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "Format email invalide.")]
    public string Email { get; set; } = string.Empty;
}

// Parametres de filtre — GET /api/tenant/companies/{id}/experts
// Exemple URL : ?page=1&pageSize=20&isActive=true&search=dupont
public class ExpertFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool? IsActive { get; set; }      // null = tous
    public string? Search { get; set; }      // cherche dans nom + email
}

// ─── REPONSES ────────────────────────────────────────────────

public class ExpertResponseDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsInvited { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Poste { get; set; }        
    public Guid? OffreId { get; set; }        
    public string? OffreTitre { get; set; }  
}

public class ExpertListResponseDto
{
    public List<ExpertResponseDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
public class AssignOffreDto
{
    public Guid? OffreId { get; set; }
    public string? Poste { get; set; }
}
public class UpdateProfilExpertDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string Email     { get; set; } = string.Empty;
    public string? Phone    { get; set; }
    public string? Specialty { get; set; }
}

public class ProfilExpertDto
{
    public Guid   Id        { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string Email     { get; set; } = string.Empty;
    public string? Phone    { get; set; }
    public string? Specialty { get; set; }
}