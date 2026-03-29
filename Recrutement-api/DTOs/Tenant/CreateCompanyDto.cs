// DTOs/Tenant/CreateCompanyDto.cs
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Tenant
{
    public class CreateCompanyDto
    {
        [Required]
        public string Nom { get; set; } = string.Empty;

        [Required]
        public string Secteur { get; set; } = string.Empty;

        [Required]
        public string RNE { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}