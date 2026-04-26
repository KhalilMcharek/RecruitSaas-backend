using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs;
using Recrutement_api.Services.AI;
using Recrutement_api.Services.Quiz;
using System;
using System.Threading.Tasks;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly IAiOrchestratorService _aiOrchestratorService;

        public QuizController(
            IQuizService quizService,
            IAiOrchestratorService aiOrchestratorService)
        {
            _quizService = quizService;
            _aiOrchestratorService = aiOrchestratorService;
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> ScheduleQuiz([FromBody] ScheduleQuizRequestDto dto)
        {
            try
            {
                var result = await _quizService.ScheduleQuizAsync(dto);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("by-token/{token}")]
        public async Task<IActionResult> GetQuizByToken(string token)
        {
            try
            {
                var result = await _quizService.GetQuizByTokenAsync(token);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("submit-result")]
        public async Task<IActionResult> SubmitResult([FromBody] SubmitQuizResultDto dto)
        {
            try
            {
                var result = await _quizService.SubmitResultAsync(dto);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("result/{quizToken}")]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> GetQuizResult(string quizToken)
        {
            try
            {
                var result = await _quizService.GetQuizResultAsync(quizToken);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("candidature/{candidatureId}")]
        public async Task<IActionResult> GetQuizByCandidature(Guid candidatureId)
        {
            var result = await _quizService.GetQuizByCandidatureAsync(candidatureId);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("notifications/{candidatId}")]
        public async Task<IActionResult> GetQuizNotifications(Guid candidatId)
        {
            var result = await _quizService.GetQuizNotificationsAsync(candidatId);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("confirm-complete")]
        public async Task<IActionResult> ConfirmQuizCompletion([FromBody] ConfirmCompletionDto dto)
        {
            try
            {
                var result = await _quizService.ConfirmQuizCompletionAsync(dto);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ⭐ NOUVEAU: Génération par l'IA basée sur l'offre
        [HttpPost("generate/{offreId}")]
        public async Task<IActionResult> GenerateQuizForOffer(
            Guid offreId,
            [FromQuery] int numQuestions = 10,
            [FromQuery] int timePerQuestion = 60)
        {
            try
            {
                var result = await _aiOrchestratorService.GenerateQuizAsync(offreId, numQuestions, timePerQuestion);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}