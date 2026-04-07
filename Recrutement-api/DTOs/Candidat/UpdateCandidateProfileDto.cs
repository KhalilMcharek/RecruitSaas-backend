using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Candidate
{
    public class UpdateCandidateProfileDto
    {
        public string? FullName { get; set; }
        [EmailAddress] public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public string? Bio { get; set; }
        public string? Seeking { get; set; }
        public string? Education { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Experience { get; set; }
        public string? Availability { get; set; }
        public List<string>? Skills { get; set; }
        public string? Linkedin { get; set; }
        public string? Github { get; set; }
        public string? PortfolioUrl { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}