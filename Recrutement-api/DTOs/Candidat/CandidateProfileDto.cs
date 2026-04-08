// ─── DTOs/Candidate/CandidateProfileDto.cs ───────────────────────────────────

namespace Recrutement_api.DTOs.Candidate
{
    // Ce que le frontend reçoit (GET)
    public class CandidateProfileDto
    {
        // Lu depuis Utilisateurs via jointure
        public string FullName { get; set; } = "";
        public string Email    { get; set; } = "";

        // Champs du profil
        public string? Phone        { get; set; }
        public string? Location     { get; set; }
        public string? Bio          { get; set; }
        public string? Seeking      { get; set; }
        public string? Education    { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Experience   { get; set; }
        public string? Availability { get; set; }
        public List<string> Skills  { get; set; } = new();
        public string? Linkedin     { get; set; }
        public string? Github       { get; set; }
        public string? PortfolioUrl { get; set; }
        public string? AvatarUrl    { get; set; }
    }
}


