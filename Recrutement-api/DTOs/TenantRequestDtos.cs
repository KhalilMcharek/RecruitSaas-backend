namespace Recrutement_api.DTOs.TenantRequest
{
    /// <summary>
    /// DTO pour la liste des demandes (vue allégée)
    /// </summary>
    public class TenantRequestListDto
    {
        public string? CompanyName { get; set; }
        public string? RNE         { get; set; }
        public string? Owner       { get; set; }  // Nom du Tenant
        public string? Industry    { get; set; }  // Secteur de l'entreprise
        public string? Status      { get; set; }
    }

    /// <summary>
    /// DTO pour le détail complet d'une demande
    /// </summary>
    public class TenantRequestDetailDto
    {
        public Guid     Id          { get; set; }
        public string?  CompanyName { get; set; }
        public string?  RNE         { get; set; }
        public string?  Owner       { get; set; }  // Nom du Tenant
        public string?  WorkEmail   { get; set; }  // Email du Tenant (Utilisateur)
        public string?  Industry    { get; set; }  // Secteur
        public string?  Description { get; set; }  // Description de l'entreprise
        public string?  LogoUrl     { get; set; }  // Logo
        public string?  Status      { get; set; }
        public DateTime CreatedAt   { get; set; }  // CreeLe
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