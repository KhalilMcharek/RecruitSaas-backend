// Controllers/QuizController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Models;
using Recrutement_api.Services;
using System.Security.Claims;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public QuizController(
            ApplicationDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // Helper: extraire CandidatId du JWT
        private Guid? GetCandidatIdFromToken()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("candidatId")
                     ?? User.FindFirstValue("nameid")
                     ?? User.FindFirstValue("sub");

            return Guid.TryParse(claim, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────────────
        // POST /api/quiz/schedule
        // Planifie un quiz ET crée la notification in-app
        // ─────────────────────────────────────────────────────
        [HttpPost("schedule")]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> ScheduleQuiz([FromBody] ScheduleQuizRequestDto dto)
        {
            // 1. Valider la candidature
            var candidature = await _context.Candidatures
                .Include(c => c.Candidat)
                .Include(c => c.Offre)
                .FirstOrDefaultAsync(c => c.Id == dto.CandidatureId);

            if (candidature == null || candidature.Candidat == null)
                return NotFound(new { message = "Candidature introuvable" });

            // 2. Générer un token unique pour le quiz
            var quizToken = $"quiz_{Guid.NewGuid():N}";

            // 3. Convertir les questions en QuizQuestionData
            var questionsData = dto.Questions.Select(q => new QuizQuestionData
            {
                Id = q.Id,
                Question = q.Question,
                ChoiceA = q.ChoiceA,
                ChoiceB = q.ChoiceB,
                ChoiceC = q.ChoiceC,
                CorrectIndex = q.CorrectIndex,
                Explanation = q.Explanation,
                TimeLimit = q.TimeLimit
            }).ToList();

            // 4. Sauvegarder le quiz en DB (avec questions en JSON)
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                CandidatureId = dto.CandidatureId,
                QuizToken = quizToken,
                ScheduledDate = dto.ScheduledDate,
                TimePerQuestion = dto.TimePerQuestion,
                Instructions = dto.Instructions,
                Questions = questionsData,  // ★ Conversion automatique en JSON
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            // 5. ★ Créer la notification in-app pour le candidat
            await _notificationService.NotifyQuizScheduledAsync(
                candidatId:     candidature.Candidat.Id,
                candidatureId:  candidature.Id,
                offreTitre:     candidature.Offre?.Titre ?? "Unknown Position",
                scheduledDate:  dto.ScheduledDate,
                quizToken:      quizToken,
                timePerQuestion: dto.TimePerQuestion
            );

            return Ok(new 
            { 
                success = true, 
                quizToken,
                message = "Quiz scheduled and notification sent"
            });
        }

        // ─────────────────────────────────────────────────────
        // GET /api/quiz/by-token/{token}
        // Récupérer un quiz par son token (pour le candidat)
        // ─────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("by-token/{token}")]
        public async Task<IActionResult> GetQuizByToken(string token)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(q => q.QuizToken == token);

            if (quiz == null)
                return NotFound(new { message = "Quiz not found" });

            return Ok(new
            {
                quiz.Id,
                quiz.ScheduledDate,
                quiz.TimePerQuestion,
                quiz.Instructions,
                Questions = quiz.Questions.Select(q => new
                {
                    q.Id,
                    q.Question,
                    q.ChoiceA,
                    q.ChoiceB,
                    q.ChoiceC,
                    q.TimeLimit
                }),
                CandidateName = quiz.Candidature?.Candidat?.Utilisateur?.Email ?? "Candidate",
                TitreOffre = quiz.Candidature?.Offre?.Titre ?? ""
            });
        }
    }

    // DTOs pour la requête
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
        public string? Explanation { get; set; }
        public int TimeLimit { get; set; } = 60;
    }
}