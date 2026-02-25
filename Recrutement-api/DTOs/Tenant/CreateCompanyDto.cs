// DTOs/Tenant/CreateCompanyDto.cs
using System.ComponentModel.DataAnnotations;

namespace Recrutement_api.DTOs.Tenant
{
    public class CreateCompanyDto
    {
        [Required]
        public string Nom { get; set; }
        
        [Required]
        public string Secteur { get; set; }
        
        [Required]
        public string RNE { get; set; }
        
        public string Description { get; set; }
    }
}