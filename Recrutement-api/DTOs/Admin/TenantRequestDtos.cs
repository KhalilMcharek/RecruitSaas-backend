// DTOs/TenantRequest/TenantRequestDtos.cs
namespace Recrutement_api.DTOs.TenantRequest
{
    public class TenantRequestListDto
    {
        public string? CompanyName { get; set; }
        public string? RNE { get; set; }
        public string? Owner { get; set; }
        public string? Industry { get; set; }
        public string? Status { get; set; }
        public string? LogoUrl { get; set; }
    }

    public class TenantRequestDetailDto
    {
        public Guid Id { get; set; }
        public string? CompanyName { get; set; }
        public string? RNE { get; set; }
        public string? Owner { get; set; }
        public string? WorkEmail { get; set; }
        public string? Industry { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TenantStatusResponseDto
    {
        public string? CompanyName { get; set; }
        public string? Status { get; set; }
    }
}