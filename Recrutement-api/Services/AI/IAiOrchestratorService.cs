using System;
using System.Threading.Tasks;

namespace Recrutement_api.Services.AI
{
    public interface IAiOrchestratorService
    {
        Task<object> CalculateScoreAsync(Guid candidatureId);
        Task<object> ProfileFromCvAsync(Guid candidatureId);
        Task<object> ClassifyCandidateAsync(Guid candidatureId);
        Task<object> SummarizeCvAsync(Guid candidatureId);
        Task<object> ExtractCompetencesAsync(Guid candidatureId);
        Task<object> ExtractExperienceAsync(Guid candidatureId);
        Task<object> ExtractCompaniesAsync(Guid candidatureId);  // TICKET-023
    }
}