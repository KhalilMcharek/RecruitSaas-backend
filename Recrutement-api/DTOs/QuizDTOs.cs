using System;
using System.Collections.Generic;

namespace Recrutement_api.DTOs
{
    public class ScheduleQuizRequestDto
    {
        public Guid CandidatureId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public int TimePerQuestion { get; set; } = 60;
        public string? Instructions { get; set; }
        public List<QuizQuestionDto> Questions { get; set; } = new();
    }

    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public string ChoiceA { get; set; } = "";
        public string ChoiceB { get; set; } = "";
        public string ChoiceC { get; set; } = "";
        public int CorrectIndex { get; set; }
        public int TimeLimit { get; set; } = 60;
    }

    public class SubmitQuizResultDto
    {
        public string QuizToken { get; set; } = "";
        public int Score { get; set; }
        public int Total { get; set; }
        public float Percentage { get; set; }
        public string Grade { get; set; } = "";
        public List<int> Answers { get; set; } = new();
        public List<int> TimeSpent { get; set; } = new();
    }

    public class ConfirmCompletionDto
    {
        public string QuizToken { get; set; } = "";
        public bool Completed { get; set; } = true;
        public DateTime? ConfirmedAt { get; set; }
        
        public int? Score { get; set; }
        public int? Total { get; set; }
        public float? Percentage { get; set; }
        public string? Grade { get; set; }
        public int? TotalTimeSec { get; set; }
        public List<QuizResultDetailDto>? Results { get; set; }
    }

    public class QuizResultDetailDto
    {
        public int QuestionId { get; set; }
        public string Question { get; set; } = "";
        public List<string> Choices { get; set; } = new();
        public int? CandidateAnswerIndex { get; set; }
        public int CorrectIndex { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
        public int TimeSpent { get; set; }
    }
}
