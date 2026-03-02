using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Recrutement_api.Models;

namespace Recrutement_api.DTOs
{
    // ==========================
    // CANDIDATE REGISTER DTO
    // ==========================
    public class RegisterDto
    {
        [Required]
        public string Nom { get; set; } = string.Empty;        // Full Name (interface)

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;      // Professional Email

        [Required]
        [MinLength(8)]
        public string MotDePasse { get; set; } = string.Empty;  // Password

        [Required]
        [Compare(nameof(MotDePasse), ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmMotDePasse { get; set; } = string.Empty; // Confirm Password
    }

    // ==========================
    // LOGIN DTO (même pour tous les rôles)
    // ==========================
    public class LoginDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string MotDePasse { get; set; } = string.Empty;
    }

    // ==========================
    // COMPANY REGISTER DTO
    // ==========================
    public class CompanyRegisterDto
    {
        [Required]
        public string TenantName { get; set; } = string.Empty;   // → Utilisateur.Nom (relation Tenant→User)

        [Required]
        public string CompanyName { get; set; } = string.Empty;   // → Entreprise.Nom

        [Required]
        public string RNE { get; set; } = string.Empty;           // → Entreprise.RNE

        [Required]
        [EmailAddress]
        public string WorkEmail { get; set; } = string.Empty;     // → Utilisateur.Email

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;      // → Utilisateur.MotDePasseHash

        [Required]
        public string Industry { get; set; } = string.Empty;      // → Entreprise.Secteur
    }

    // ==========================
    // AUTH RESPONSE DTO (JWT)
    // ==========================
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}