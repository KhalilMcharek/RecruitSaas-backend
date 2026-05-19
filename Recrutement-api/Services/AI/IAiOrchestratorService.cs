using System;
using System.Threading.Tasks;

namespace Recrutement_api.Services.AI
{
    public interface IAiOrchestratorService
    {
        Task<object> CalculateScoreAsync(Guid candidatureId);
        Task<object> ClassifyCandidateAsync(Guid candidatureId);
        Task<object> SummarizeCvAsync(Guid candidatureId);
        Task<object> ExtractCompetencesAsync(Guid candidatureId);
        Task<object> ExtractExperienceAsync(Guid candidatureId);
        Task<object> ExtractCompaniesAsync(Guid candidatureId);
        Task<object> ExtractCertificationsAsync(Guid candidatureId);
        Task<object> GenerateQuizAsync(Guid offreId, int numQuestions = 10, int timePerQuestion = 60);
        Task<object> GenerateInterviewQuestionsAsync(Guid candidatureId, int numQuestions = 5, string langue = "FR");
        Task<object> GenerateTtsAsync(string text, string langue = "FR");
        Task<object> GenerateSttAsync(System.IO.Stream audioStream, string filename);
        // ReplyInterviewAsync disabled — simplified interview uses pre-generated questions only
        // Task<object> ReplyInterviewAsync(Guid entretienId, string questionActuelle, string reponseCandidat, string langue = "FR");
        Task<object> EvaluateInterviewAsync(Guid entretienId);
    }
}