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

        
    }
}