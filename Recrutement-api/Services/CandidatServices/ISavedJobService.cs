public interface ISavedJobService
{
    Task<IEnumerable<SavedJobDto>> GetSavedJobsAsync(Guid candidatId);
    Task<SavedJobDto> SaveJobAsync(Guid candidatId, Guid offreId);
    Task RemoveSavedJobAsync(Guid candidatId, Guid offreId);
    Task<bool> IsJobSavedAsync(Guid candidatId, Guid offreId);
}