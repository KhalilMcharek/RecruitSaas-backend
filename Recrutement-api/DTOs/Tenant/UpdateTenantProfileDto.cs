using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Tenant
{
    public class UpdateTenantProfileDto
    {
        // Company Info
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? CompanySize { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        
        // Online Presence
        public string? Website { get; set; }
        public string? Linkedin { get; set; }
        public string? Twitter { get; set; }
        
        // Recruiter Account
        public string? FullName { get; set; }
        
        [EmailAddress]
        public string? Email { get; set; }
        
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }
        
        // Hiring Preferences
        public string? HiringStatus { get; set; } // "actively", "sometimes", "paused"
        public List<string>? WorkTypes { get; set; } // ["Full-time", "Remote", ...]
        public List<string>? TechStack { get; set; } // ["React", "Node.js", ...]
        
        // Password change (optional)
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}