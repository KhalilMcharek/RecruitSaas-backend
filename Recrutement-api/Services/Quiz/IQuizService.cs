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
        Task<object> SubmitResultAsync(SubmitQuizResultDto dto);
        Task<object> GetQuizResultAsync(string quizToken);
        Task<object> GetQuizByCandidatureAsync(Guid candidatureId);
        Task<IEnumerable<object>> GetQuizNotificationsAsync(Guid candidatId);
        Task<object> ConfirmQuizCompletionAsync(ConfirmCompletionDto dto);
    }
}
