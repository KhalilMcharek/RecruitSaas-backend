using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;

namespace Recrutement_api.Services.Quiz
{
    public class QuizService : IQuizService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public QuizService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<object> ScheduleQuizAsync(ScheduleQuizRequestDto dto)
        {
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat)
                    .ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == dto.CandidatureId);

            if (candidature == null || candidature.Candidat == null)
                throw new KeyNotFoundException("Candidature introuvable");

            var quizToken = $"quiz_{Guid.NewGuid():N}";

            var questionsData = dto.Questions.Select(q => new QuizQuestionData
            {
                Id           = q.Id,
                Question     = q.Question,
                ChoiceA      = q.ChoiceA,
                ChoiceB      = q.ChoiceB,
                ChoiceC      = q.ChoiceC,
                CorrectIndex = q.CorrectIndex,
                TimeLimit    = q.TimeLimit
            }).ToList();

            var quiz = new Models.Quiz
            {
                Id              = Guid.NewGuid(),
                CandidatureId   = dto.CandidatureId,
                QuizToken       = quizToken,
                ScheduledDate   = dto.ScheduledDate,
                TimePerQuestion = dto.TimePerQuestion,
                Instructions    = dto.Instructions,
                Questions       = questionsData,
                CreatedAt       = DateTime.UtcNow,
                CompletedAt     = null
            };

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyQuizScheduledAsync(
                candidatId:      candidature.Candidat.Id,
                candidatureId:   candidature.Id,
                offreTitre:      candidature.Offre?.Titre ?? "Unknown Position",
                scheduledDate:   dto.ScheduledDate,
                quizToken:       quizToken,
                timePerQuestion: dto.TimePerQuestion
            );

            return new
            {
                success   = true,
                quizToken = quizToken,
                quizUrl   = $"/quiz/{quizToken}",
                message   = "Quiz scheduled and notification sent to candidate"
            };
        }

        public async Task<object> GetQuizByTokenAsync(string token)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                        .ThenInclude(ca => ca.Utilisateur)
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(q => q.QuizToken == token);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            if (quiz.CompletedAt != null)
                throw new InvalidOperationException("This quiz has already been completed.");

            return new
            {
                quiz.Id,
                quiz.ScheduledDate,
                quiz.TimePerQuestion,
                quiz.Instructions,
                quiz.CompletedAt,
                CandidateName = quiz.Candidature?.Candidat?.Utilisateur?.Nom
                             ?? quiz.Candidature?.Candidat?.Utilisateur?.Email
                             ?? "Candidate",
                TitreOffre = quiz.Candidature?.Offre?.Titre ?? "",
                Questions = quiz.Questions.Select(q => new
                {
                    q.Id,
                    q.Question,
                    q.ChoiceA,
                    q.ChoiceB,
                    q.ChoiceC,
                    q.TimeLimit
                })
            };
        }

        public async Task<object> SubmitResultAsync(SubmitQuizResultDto dto)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                .FirstOrDefaultAsync(q => q.QuizToken == dto.QuizToken);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            if (quiz.CompletedAt != null)
                throw new InvalidOperationException("Quiz already submitted");

            quiz.CompletedAt = DateTime.UtcNow;

            // Evaluate the quiz locally on the backend
            int score = 0;
            var detailedResults = new List<object>();
            
            for (int i = 0; i < quiz.Questions.Count; i++)
            {
                var q = quiz.Questions[i];
                int cand = i < dto.Answers.Count ? dto.Answers[i] : -1;
                bool ok = cand == q.CorrectIndex && cand != -1;
                if (ok) score++;
                
                int timeSpent = i < dto.TimeSpent.Count ? dto.TimeSpent[i] : 0;

                detailedResults.Add(new
                {
                    question_id = q.Id,
                    question = q.Question,
                    choices = new[] { q.ChoiceA, q.ChoiceB, q.ChoiceC },
                    candidate_answer_index = cand,
                    correct_index = q.CorrectIndex,
                    is_correct = ok,
                    explanation = "",
                    time_spent = timeSpent
                });
            }

            int total = quiz.Questions.Count;
            float percentage = total > 0 ? (float)Math.Round(((float)score / total) * 100) : 0;
            string grade = percentage >= 90 ? "A" : percentage >= 75 ? "B" : percentage >= 60 ? "C" : percentage >= 50 ? "D" : "F";
            int totalTimeSec = dto.TimeSpent.Sum();

            var quizResult = new QuizResult
            {
                Id            = Guid.NewGuid(),
                QuizId        = quiz.Id,
                CandidatId    = quiz.Candidature?.CandidatId ?? Guid.Empty,
                CandidatureId = quiz.CandidatureId,
                QuizToken     = dto.QuizToken,
                Score         = score,
                Total         = total,
                Percentage    = percentage,
                Grade         = grade,
                AnswersJson   = System.Text.Json.JsonSerializer.Serialize(dto.Answers),
                TimeSpentJson = System.Text.Json.JsonSerializer.Serialize(dto.TimeSpent),
                CompletedAt   = DateTime.UtcNow,
            };

            _context.QuizResults.Add(quizResult);
            await _context.SaveChangesAsync();

            return new
            {
                success  = true,
                message  = "Result saved",
                resultId = quizResult.Id,
                score = score,
                percentage = percentage,
                grade = grade,
                totalTimeSec = totalTimeSec,
                timeouts = dto.Answers.Count(a => a == -1),
                results = detailedResults
            };
        }

        public async Task<object> GetQuizResultAsync(string quizToken)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                        .ThenInclude(ca => ca.Utilisateur)
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(q => q.QuizToken == quizToken);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            var result = await _context.QuizResults
                .FirstOrDefaultAsync(r => r.QuizToken == quizToken);

            if (result == null)
                return new
                {
                    completed  = false,
                    quizToken  = quizToken,
                    candidateName = quiz.Candidature?.Candidat?.Utilisateur?.Nom ?? "Candidate",
                    titreOffre    = quiz.Candidature?.Offre?.Titre ?? "",
                    scheduledDate = quiz.ScheduledDate,
                };

            var detailedResults = quiz.Questions.Select((q, i) =>
            {
                var answers = System.Text.Json.JsonSerializer.Deserialize<List<int>>(result.AnswersJson) ?? new List<int>();
                var times   = System.Text.Json.JsonSerializer.Deserialize<List<int>>(result.TimeSpentJson) ?? new List<int>();
                var cand    = i < answers.Count ? answers[i] : -1;
                var ok      = cand == q.CorrectIndex && cand != -1;
                return new
                {
                    question_id            = q.Id,
                    question               = q.Question,
                    choices                = new[] { q.ChoiceA, q.ChoiceB, q.ChoiceC },
                    candidate_answer_index = cand,
                    correct_index          = q.CorrectIndex,
                    is_correct             = ok,
                    time_spent             = i < times.Count ? times[i] : 0,
                };
            }).ToList();

            return new
            {
                completed     = true,
                quizToken     = quizToken,
                candidateName = quiz.Candidature?.Candidat?.Utilisateur?.Nom ?? "Candidate",
                titreOffre    = quiz.Candidature?.Offre?.Titre ?? "",
                scheduledDate = quiz.ScheduledDate,
                completedAt   = result.CompletedAt,
                score         = result.Score,
                total         = result.Total,
                percentage    = result.Percentage,
                grade         = result.Grade,
                results       = detailedResults,
                totalTimeSec  = System.Text.Json.JsonSerializer.Deserialize<List<int>>(result.TimeSpentJson)?.Sum() ?? 0,
            };
        }

        public async Task<object> GetQuizByCandidatureAsync(Guid candidatureId)
        {
            var quiz = await _context.Quizzes
                .FirstOrDefaultAsync(q => q.CandidatureId == candidatureId);

            if (quiz == null)
                return new { exists = false };

            var result = await _context.QuizResults
                .FirstOrDefaultAsync(r => r.QuizToken == quiz.QuizToken);

            return new
            {
                exists        = true,
                quizToken     = quiz.QuizToken,
                scheduledDate = quiz.ScheduledDate,
                completed     = quiz.CompletedAt != null,
                completedAt   = quiz.CompletedAt,
                score         = result?.Score,
                total         = result?.Total,
                percentage    = result?.Percentage,
                grade         = result?.Grade,
            };
        }

        public async Task<IEnumerable<object>> GetQuizNotificationsAsync(Guid candidatId)
        {
            return await _context.CandidatNotifications
                .Where(n => n.CandidatId == candidatId && n.Type == "quiz_invitation")
                .OrderByDescending(n => n.CreeLe)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Body,
                    n.IsRead,
                    n.CreeLe,
                    n.QuizToken,
                    n.CandidatureId,
                    QuizUrl = $"/quiz/{n.QuizToken}"
                })
                .ToListAsync();
        }

        public async Task<object> ConfirmQuizCompletionAsync(ConfirmCompletionDto dto)
        {
            var quiz = await _context.Quizzes
                .FirstOrDefaultAsync(q => q.QuizToken == dto.QuizToken);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            quiz.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new
            {
                success = true,
                message = "Quiz completion confirmed",
                completedAt = quiz.CompletedAt
            };
        }
    }
}
