using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Admin
{
    /// <summary>Profil tenant lu depuis wwwroot/data/tenant-profiles/*.json</summary>
    public class TenantProfileFromFileDto
    {
        public string? CompanyName { get; set; }
        public string? RNE { get; set; }
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Industry { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Status { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class TenantListDto
    {
        public Guid TenantId { get; set; }
        public string? TenantDisplayName { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string RNE { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Phone { get; set; }
        public string Industry { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int ActiveJobs { get; set; }
        public string? Status { get; set; }
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
        public string CompanyName { get; set; } = string.Empty;
        public string RNE { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Phone { get; set; }
        public string Industry { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int ActiveJobs { get; set; }
        public string? Status { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
        public string? EmployeeCount { get; set; }
        public string? JobTitle { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        public string? HiringStatus { get; set; }
        public string? WorkTypesJson { get; set; }
        public string? TechStackJson { get; set; }
        public DateTime? ProfileUpdatedAt { get; set; }
    }

    public class AdminProfileDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateAdminProfileDto
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class AdminChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
