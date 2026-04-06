namespace Recrutement_api.DTOs.Tenant
{
    public class UpdateTenantProfileDto
    {
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }

        public string? Website { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        public string? HiringStatus { get; set; }

        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }
    }
}