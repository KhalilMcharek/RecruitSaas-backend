// Models/Quiz.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Recrutement_api.Models
{
    [Table("Quizzes")]
    public class Quiz
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid CandidatureId { get; set; }
        
        [ForeignKey("CandidatureId")]
        public Candidature? Candidature { get; set; }
        
        [Required, StringLength(100)]
        public string QuizToken { get; set; } = "";
        
        [Required]
        public DateTime ScheduledDate { get; set; }
        
        [Required]
        public int TimePerQuestion { get; set; } = 60;
        
        public string? Instructions { get; set; }
        
        // ★ CHANGEMENT: Utiliser "text" pour PostgreSQL au lieu de "nvarchar(max)"
        [Column(TypeName = "text")]
        public string QuestionsJson { get; set; } = "[]";
        
        [NotMapped]
        public List<QuizQuestionData> Questions
        {
            get => JsonSerializer.Deserialize<List<QuizQuestionData>>(QuestionsJson) ?? new List<QuizQuestionData>();
            set => QuestionsJson = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
        }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
    }

    public class QuizQuestionData
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public string ChoiceA { get; set; } = "";
        public string ChoiceB { get; set; } = "";
        public string ChoiceC { get; set; } = "";
        public int CorrectIndex { get; set; }
        public int TimeLimit { get; set; } = 60;
    }
}