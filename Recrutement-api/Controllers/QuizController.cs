// Controllers/QuizController.cs — VERSION COMPLÈTE CORRIGÉE
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
    public class QuizController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService  _notificationService;

        public QuizController(
            ApplicationDbContext context,
            NotificationService  notificationService)
        {
            _context             = context;
            _notificationService = notificationService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/quiz/schedule   [Tenant]
        // Crée le quiz, stocke en base, envoie notification in-app au candidat
        // ─────────────────────────────────────────────────────────────────────
        // Controllers/QuizController.cs
[HttpPost("schedule")]
public async Task<IActionResult> ScheduleQuiz([FromBody] ScheduleQuizRequestDto dto)
{
    System.Diagnostics.Debug.WriteLine($"[QuizController] 🚀 ScheduleQuiz called with CandidatureId={dto.CandidatureId}");

    // 1️⃣ Récupérer la candidature avec ses relations
    var candidature = await _context.Candidatures
        .Include(c => c.Candidat)
            .ThenInclude(ca => ca.Utilisateur)
        .Include(c => c.Offre)
        .FirstOrDefaultAsync(c => c.Id == dto.CandidatureId);

    System.Diagnostics.Debug.WriteLine($"[QuizController] 🔍 Candidature found: {candidature != null}");
    
    if (candidature == null || candidature.Candidat == null)
    {
        System.Diagnostics.Debug.WriteLine($"[QuizController] ❌ Candidature or Candidat is null");
        return NotFound(new { message = "Candidature introuvable" });
    }

    System.Diagnostics.Debug.WriteLine($"[QuizController] 👤 Candidat.Id = {candidature.Candidat.Id}");
    System.Diagnostics.Debug.WriteLine($"[QuizController] 👤 Candidat.UtilisateurId = {candidature.Candidat.UtilisateurId}");

    // 2️⃣ Générer un token unique pour le quiz
    var quizToken = $"quiz_{Guid.NewGuid():N}";
    System.Diagnostics.Debug.WriteLine($"[QuizController] 🎫 Generated quizToken: {quizToken}");

    // 3️⃣ Mapper les questions depuis le DTO vers le modèle
    var questionsData = dto.Questions.Select(q => new QuizQuestionData
    {
        Id           = q.Id,
        Question     = q.Question,
        ChoiceA      = q.ChoiceA,
        ChoiceB      = q.ChoiceB,
        ChoiceC      = q.ChoiceC,
        CorrectIndex = q.CorrectIndex,
        Explanation  = q.Explanation,
        TimeLimit    = q.TimeLimit
    }).ToList();

    // 4️⃣ ★ CRÉER L'ENTITÉ QUIZ (c'est cette ligne qui manquait !)
    var quiz = new Quiz
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

    // 5️⃣ Sauvegarder le quiz en base
    _context.Quizzes.Add(quiz);
    await _context.SaveChangesAsync();
    System.Diagnostics.Debug.WriteLine($"[QuizController] ✅ Quiz saved to DB, Id={quiz.Id}");

    // 6️⃣ ★ Envoyer la notification au candidat
    System.Diagnostics.Debug.WriteLine($"[QuizController] 📢 Calling NotifyQuizScheduledAsync...");
    
    await _notificationService.NotifyQuizScheduledAsync(
        candidatId:      candidature.Candidat.Id,
        candidatureId:   candidature.Id,
        offreTitre:      candidature.Offre?.Titre ?? "Unknown Position",
        scheduledDate:   dto.ScheduledDate,
        quizToken:       quizToken,
        timePerQuestion: dto.TimePerQuestion
    );
    
    System.Diagnostics.Debug.WriteLine($"[QuizController] 📢 NotifyQuizScheduledAsync returned");

    // 7️⃣ Retourner la réponse au tenant
    return Ok(new
    {
        success   = true,
        quizToken = quizToken,
        quizUrl   = $"/quiz/{quizToken}",
        message   = "Quiz scheduled and notification sent to candidate"
    });
}
        // ─────────────────────────────────────────────────────────────────────
        // GET /api/quiz/by-token/{token}   [AllowAnonymous]
        // Retourne les données du quiz — SANS correct_index ni explanation
        // pour ne pas tricher côté candidat
        // ─────────────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("by-token/{token}")]
        public async Task<IActionResult> GetQuizByToken(string token)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                        .ThenInclude(ca => ca.Utilisateur)
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(q => q.QuizToken == token);

            if (quiz == null)
                return NotFound(new { message = "Quiz not found" });

            // ★ Empêcher double passage
            if (quiz.CompletedAt != null)
                return BadRequest(new { message = "This quiz has already been completed." });

            return Ok(new
            {
                quiz.Id,
                quiz.ScheduledDate,       // ★ Retourné pour le verrouillage côté Vue
                quiz.TimePerQuestion,
                quiz.Instructions,
                quiz.CompletedAt,
                CandidateName = quiz.Candidature?.Candidat?.Utilisateur?.Nom
                             ?? quiz.Candidature?.Candidat?.Utilisateur?.Email
                             ?? "Candidate",
                TitreOffre = quiz.Candidature?.Offre?.Titre ?? "",
                // ★ On envoie les questions SANS correctIndex ni explanation
                Questions = quiz.Questions.Select(q => new
                {
                    q.Id,
                    q.Question,
                    q.ChoiceA,
                    q.ChoiceB,
                    q.ChoiceC,
                    q.TimeLimit
                    // correctIndex intentionnellement absent
                    // explanation intentionnellement absente
                })
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/quiz/submit-result   [AllowAnonymous]
        // ★ Stocke les résultats dans QuizResult (table dédiée)
        // ─────────────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpPost("submit-result")]
        public async Task<IActionResult> SubmitResult([FromBody] SubmitQuizResultDto dto)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                .FirstOrDefaultAsync(q => q.QuizToken == dto.QuizToken);

            if (quiz == null)
                return NotFound(new { message = "Quiz not found" });

            if (quiz.CompletedAt != null)
                return BadRequest(new { message = "Quiz already submitted" });

            // ★ Marquer le quiz comme complété
            quiz.CompletedAt = DateTime.UtcNow;

            // ★ Créer l'enregistrement QuizResult
            var quizResult = new QuizResult
            {
                Id            = Guid.NewGuid(),
                QuizId        = quiz.Id,
                CandidatId    = quiz.Candidature?.CandidatId ?? Guid.Empty,
                CandidatureId = quiz.CandidatureId,
                QuizToken     = dto.QuizToken,
                Score         = dto.Score,
                Total         = dto.Total,
                Percentage    = dto.Percentage,
                Grade         = dto.Grade,
                AnswersJson   = System.Text.Json.JsonSerializer.Serialize(dto.Answers),
                TimeSpentJson = System.Text.Json.JsonSerializer.Serialize(dto.TimeSpent),
                CompletedAt   = DateTime.UtcNow,
            };

            _context.QuizResults.Add(quizResult);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success  = true,
                message  = "Result saved",
                resultId = quizResult.Id
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/quiz/result/{quizToken}   [Tenant]
        // ★ Le tenant récupère le résultat d'un candidat pour générer le PDF
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("result/{quizToken}")]
        [Authorize(Roles = "Tenant,Admin")]
        public async Task<IActionResult> GetQuizResult(string quizToken)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Candidat)
                        .ThenInclude(ca => ca.Utilisateur)
                .Include(q => q.Candidature)
                    .ThenInclude(c => c.Offre)
                .FirstOrDefaultAsync(q => q.QuizToken == quizToken);

            if (quiz == null)
                return NotFound(new { message = "Quiz not found" });

            var result = await _context.QuizResults
                .FirstOrDefaultAsync(r => r.QuizToken == quizToken);

            if (result == null)
                return Ok(new
                {
                    completed  = false,
                    quizToken  = quizToken,
                    candidateName = quiz.Candidature?.Candidat?.Utilisateur?.Nom ?? "Candidate",
                    titreOffre    = quiz.Candidature?.Offre?.Titre ?? "",
                    scheduledDate = quiz.ScheduledDate,
                });

            // Reconstruire les résultats détaillés en comparant avec les vraies réponses
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
                    explanation            = q.Explanation,
                    time_spent             = i < times.Count ? times[i] : 0,
                };
            }).ToList();

            return Ok(new
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
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/quiz/candidature/{candidatureId}   [Tenant]
        // Renvoie le quiz d'une candidature (pour savoir si quiz passé)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("candidature/{candidatureId}")]
        [Authorize(Roles = "Tenant,Admin")]
        public async Task<IActionResult> GetQuizByCandidature(Guid candidatureId)
        {
            var quiz = await _context.Quizzes
                .FirstOrDefaultAsync(q => q.CandidatureId == candidatureId);

            if (quiz == null)
                return Ok(new { exists = false });

            var result = await _context.QuizResults
                .FirstOrDefaultAsync(r => r.QuizToken == quiz.QuizToken);

            return Ok(new
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
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/quiz/notifications/{candidatId}   [AllowAnonymous]
        // Invitations quiz reçues par le candidat (utilisé par le polling Vue)
        // ─────────────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("notifications/{candidatId}")]
        public async Task<IActionResult> GetQuizNotifications(Guid candidatId)
        {
            var notifications = await _context.CandidatNotifications
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

            return Ok(notifications);
        }
    
   

// ─────────────────────────────────────────────────────────────────────
// POST /api/quiz/confirm-complete   [AllowAnonymous] ⭐ NOUVEAU
// Le candidat confirme avoir vu ses résultats → quiz marqué comme COMPLETED
// C'est SEULEMENT ici que quiz.CompletedAt est défini → active bouton PDF
// ─────────────────────────────────────────────────────────────────────
[AllowAnonymous]
[HttpPost("confirm-complete")]
public async Task<IActionResult> ConfirmQuizCompletion([FromBody] ConfirmCompletionDto dto)
{
    System.Diagnostics.Debug.WriteLine($"\n=== [QuizController] 🎯 ConfirmQuizCompletion START ===");
    System.Diagnostics.Debug.WriteLine($"[QuizController] 📥 Token: {dto.QuizToken}, Completed: {dto.Completed}");

    var quiz = await _context.Quizzes
        .Include(q => q.Candidature)
            .ThenInclude(c => c.Candidat)
        .FirstOrDefaultAsync(q => q.QuizToken == dto.QuizToken);

    if (quiz == null)
    {
        System.Diagnostics.Debug.WriteLine($"[QuizController] ❌ Quiz NOT FOUND: {dto.QuizToken}");
        return NotFound(new { message = "Quiz not found" });
    }

    System.Diagnostics.Debug.WriteLine($"[QuizController] ✅ Quiz FOUND - CompletedAt BEFORE: {quiz.CompletedAt}");

    // ★ CRITIQUE: Marquer comme completed SEULEMENT ici
    quiz.CompletedAt = DateTime.UtcNow;

    
    await _context.SaveChangesAsync();
    
    System.Diagnostics.Debug.WriteLine($"[QuizController] ✅ CompletedAt AFTER: {quiz.CompletedAt}");
    System.Diagnostics.Debug.WriteLine($"=== [QuizController] 🎯 ConfirmQuizCompletion END ===\n");

    return Ok(new
    {
        success = true,
        message = "Quiz completion confirmed",
        completedAt = quiz.CompletedAt
    });}
    
    // ── DTOs ─────────────────────────────────────────────────────────────────
    public class ScheduleQuizRequestDto
    {
        public Guid   CandidatureId   { get; set; }
        public DateTime ScheduledDate { get; set; }
        public int    TimePerQuestion { get; set; } = 60;
        public string? Instructions   { get; set; }
        public List<QuizQuestionDto> Questions { get; set; } = new();
    }

    public class QuizQuestionDto
    {
        public int     Id           { get; set; }
        public string  Question     { get; set; } = "";
        public string  ChoiceA      { get; set; } = "";
        public string  ChoiceB      { get; set; } = "";
        public string  ChoiceC      { get; set; } = "";
        public int     CorrectIndex { get; set; }
        public string? Explanation  { get; set; }
        public int     TimeLimit    { get; set; } = 60;
    }

    public class SubmitQuizResultDto
    {
        public string    QuizToken  { get; set; } = "";
        public int       Score      { get; set; }
        public int       Total      { get; set; }
        public float     Percentage { get; set; }
        public string    Grade      { get; set; } = "";
        public List<int> Answers    { get; set; } = new();
        public List<int> TimeSpent  { get; set; } = new();
    }

    // ── DTO pour la confirmation de complétion par le candidat ⭐ NOUVEAU ─────────────
public class ConfirmCompletionDto
{
    public string QuizToken { get; set; } = "";
    public bool Completed { get; set; } = true;
    public DateTime? ConfirmedAt { get; set; }
    
    // Résultats optionnels (pour mise à jour finale)
    public int? Score { get; set; }
    public int? Total { get; set; }
    public float? Percentage { get; set; }
    public string? Grade { get; set; }
    public int? TotalTimeSec { get; set; }
    public List<QuizResultDetailDto>? Results { get; set; }
}

// ── DTO pour les détails des résultats (utilisé dans le rapport PDF) ⭐ NOUVEAU ───
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
}}