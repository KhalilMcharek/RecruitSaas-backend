// DTOs/TenantProfileFromFileDto.cs
namespace Recrutement_api.DTOs
{
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
}