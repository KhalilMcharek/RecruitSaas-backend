using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Recrutement_api.DTOs;

namespace Recrutement_api.Services.Quiz
{
    public interface IQuizService
    {
        Task<object> ScheduleQuizAsync(ScheduleQuizRequestDto dto);
        Task<object> GetQuizByTokenAsync(string token);
        Task<object> StartQuizAsync(string quizToken, string fingerprint);
        Task<object> SaveProgressAsync(SaveProgressDto dto, string fingerprint);
        Task<object> SubmitResultAsync(SubmitQuizResultDto dto, string fingerprint);
        Task<object> GetQuizResultAsync(string quizToken);
        Task<object> GetQuizByCandidatureAsync(Guid candidatureId);
        Task<IEnumerable<object>> GetQuizNotificationsAsync(Guid candidatId);
    }
}
