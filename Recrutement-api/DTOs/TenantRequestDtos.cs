namespace Recrutement_api.DTOs.TenantRequest
{
    /// <summary>
    /// DTO pour la liste des demandes (vue allégée)
    /// </summary>
    public class TenantRequestListDto
    {
        public string? CompanyName { get; set; }
        public string? RNE         { get; set; }
        public string? Owner       { get; set; }
        public string? Industry    { get; set; }
        public string? Status      { get; set; }
    }

    /// <summary>
    /// DTO pour le détail d'une demande (sans Password)
    /// </summary>
    public class TenantRequestDetailDto
    {
        public Guid     Id          { get; set; }
        public string?  CompanyName { get; set; }
        public string?  RNE         { get; set; }
        public string?  Owner       { get; set; }
        public string?  WorkEmail   { get; set; }
        public string?  Industry    { get; set; }
        public string?  Status      { get; set; }
        public DateTime CreatedAt   { get; set; }
    }

    /// <summary>
    /// DTO retourné après approve/reject
    /// </summary>
    public class TenantStatusResponseDto
    {
        public string? CompanyName { get; set; }
        public string? Status      { get; set; }
    }
}