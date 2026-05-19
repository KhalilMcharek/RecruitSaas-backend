using System;
using System.Collections.Generic;

namespace Recrutement_api.DTOs
{
    public class ScheduleQuizRequestDto
    {
        public Guid CandidatureId { get; set; }
        /// <summary>When the quiz becomes accessible.</summary>
        public DateTime AvailableFrom { get; set; }
        /// <summary>Last moment the quiz can be submitted.</summary>
        public DateTime Deadline { get; set; }
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

    public class QuestionAnswerDto
    {
        public int QuestionId { get; set; }
        public int AnswerIndex { get; set; }
        public int TimeSpent { get; set; }
    }

    public class SubmitQuizResultDto
    {
        public string QuizToken { get; set; } = "";
        public List<QuestionAnswerDto> Answers { get; set; } = new();
    }

    public class SaveProgressDto
    {
        public string QuizToken { get; set; } = "";
        public List<QuestionAnswerDto> Answers { get; set; } = new();
    }

    // Removed ConfirmCompletionDto as it's no longer used.

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
