// DTOs/TenantManagementDtos.cs
using System.Text.Json.Serialization;

namespace Recrutement_api.DTOs.TenantRequest
{
    public class TenantListDto
    {
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
    }

    public class TenantDetailDto
    {
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
    }
}