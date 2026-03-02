// DTOs/Tenant/UpdateCompanyDto.cs
namespace Recrutement_api.DTOs.Tenant
{
    public class UpdateCompanyDto
    {
        public string? Nom { get; set; }
        public string? Secteur { get; set; }
        public string? RNE { get; set; }
        public string? Description { get; set; }
    }
}