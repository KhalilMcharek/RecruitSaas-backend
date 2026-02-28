using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Recrutement_api.Models;

namespace Recrutement_api.DTOs
{
    // ==========================
    // USER REGISTER DTO
    // ==========================
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string MotDePasse { get; set; } = string.Empty;

        [Required]
        public string Nom { get; set; } = string.Empty;

        public string Prenom { get; set; } = string.Empty;

        public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Candidat;
    }
    // ==========================
    // LOGIN DTO
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
    public string tenantName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string RNE { get; set; } = string.Empty;

    public string Secteur { get; set; } = string.Empty;

    public string WorkEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Prenom { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string LogoUrl { get; set; } = string.Empty;
}



    // ==========================
    // AUTH RESPONSE DTO (JWT)
    // ==========================
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public RoleUtilisateur Role { get; set; }
    }
}