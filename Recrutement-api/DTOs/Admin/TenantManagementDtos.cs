// DTOs/TenantManagementDtos.cs
using System.Text.Json.Serialization;

namespace Recrutement_api.DTOs.TenantRequest
{
    public class TenantListDto
    {
        public Guid TenantId { get; set; }
        /// <summary>Nom affiché du compte recruteur (Prénom + Nom utilisateur, ou e-mail).</summary>
        public string? TenantDisplayName { get; set; }
        public string? CompanyName { get; set; }
        public string? RNE { get; set; }
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Phone { get; set; }
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int ActiveJobs { get; set; }
        public string? Status { get; set; }

        /// <summary>Données de TenantProfiles (recruteur), si le tenant a rempli son profil.</summary>
        public string? JobTitle { get; set; }
        public string? Website { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        public string? HiringStatus { get; set; }
        public string? WorkTypesJson { get; set; }
        public string? TechStackJson { get; set; }
        public DateTime? ProfileUpdatedAt { get; set; }
    }

    public class TenantDetailDto
    {
        public Guid TenantId { get; set; }
        public string? TenantDisplayName { get; set; }
        public string? CompanyName { get; set; }
        public string? RNE { get; set; }
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Phone { get; set; }
        public string? Industry { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int ActiveJobs { get; set; }
        public string? Status { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
        public int? EmployeeCount { get; set; }

        public string? JobTitle { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        public string? HiringStatus { get; set; }
        public string? WorkTypesJson { get; set; }
        public string? TechStackJson { get; set; }
        public DateTime? ProfileUpdatedAt { get; set; }
    }
}