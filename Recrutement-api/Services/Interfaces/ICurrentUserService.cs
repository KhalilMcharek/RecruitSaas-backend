namespace Recrutement_api.Services.Interfaces
{
    public interface ICurrentUserService
    {
        Guid UserId { get; }
        string Role { get; }

        Guid? TenantId { get; }
        Guid? ExpertId { get; }
        Guid? CandidatId { get; }
    }
}
