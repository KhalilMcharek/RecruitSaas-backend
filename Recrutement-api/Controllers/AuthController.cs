using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;
using Recrutement_api.Services;

namespace Recrutement_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public AuthController(IAuthService authService, ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterCandidate([FromBody] RegisterDto dto)
        {
            try
            {
                var message = await _authService.RegisterCandidateAsync(dto);
                return Ok(new { Message = message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("company/register")]
        public async Task<IActionResult> RegisterCompany([FromBody] CompanyRegisterDto dto)
        {
            try
            {
                var message = await _authService.RegisterCompanyAsync(dto);
                return Ok(new { Message = message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var response = await _authService.LoginAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ── Changer le mot de passe (Expert + tous les rôles) ──────────
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                // Lire l'userId depuis le token JWT
                var userIdStr = User.FindFirstValue("userId")
                             ?? User.FindFirstValue("sub")
                             ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userIdStr))
                    return Unauthorized(new { message = "Token invalide." });

                var userId = Guid.Parse(userIdStr);

                var user = await _context.Utilisateurs.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "Utilisateur introuvable." });

                // Vérifier l'ancien mot de passe
                if (!_authService.VerifyPassword(dto.AncienMotDePasse, user.MotDePasseHash))
                    return BadRequest(new { message = "Mot de passe actuel incorrect." });

                // Mettre à jour
                user.MotDePasseHash = _authService.HashPassword(dto.NouveauMotDePasse);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Mot de passe mis à jour avec succès." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}