using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace Recrutement_api.Models
{
    [Table("QuizResults")]
    public class QuizResult
    {
        [Key]
        public Guid Id { get; set; }
 
        [Required]
        public Guid QuizId { get; set; }
 
        [ForeignKey("QuizId")]
        public Quiz? Quiz { get; set; }
 
        [Required]
        public Guid CandidatId { get; set; }
 
        [Required]
        public Guid CandidatureId { get; set; }
 
        [Required, StringLength(120)]
        public string QuizToken { get; set; } = "";
 
        public int   Score      { get; set; }
        public int   Total      { get; set; }
        public float Percentage { get; set; }
 
        [StringLength(2)]
        public string Grade { get; set; } = "";
 
        // Stockage JSON des réponses et temps
        [Column(TypeName = "text")]
        public string AnswersJson   { get; set; } = "[]";
 
        [Column(TypeName = "text")]
        public string TimeSpentJson { get; set; } = "[]";
 
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}
 