// ─── DTOs/Tenant/TenantProfileDto.cs ─────────────────────────────────────────

namespace Recrutement_api.DTOs.Tenant
{
    // Ce que le frontend reçoit (GET)
    public class TenantProfileDto
    {
        // Lu depuis Utilisateurs via jointure
        public string FullName { get; set; } = "";
        public string Email    { get; set; } = "";

        // Champs du profil
        public string? JobTitle      { get; set; }
        public string? Phone         { get; set; }
        public string? Website       { get; set; }
        public string? Linkedin      { get; set; }
        public string? Twitter       { get; set; }
        public string? HiringStatus  { get; set; }
        public List<string> WorkTypes { get; set; } = new();
        public List<string> TechStack { get; set; } = new();
        public string? LogoUrl       { get; set; }
    }
 
     public class ChangePasswordDto
     {
         public string CurrentPassword { get; set; } = "";
         public string NewPassword     { get; set; } = "";
         public string ConfirmPassword { get; set; } = "";
     }
 }
