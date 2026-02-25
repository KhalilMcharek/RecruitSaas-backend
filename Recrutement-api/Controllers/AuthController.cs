using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs;
using Recrutement_api.Services;

namespace Recrutement_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // =====================================================
        // REGISTER CANDIDATE
        // =====================================================
        [HttpPost("register")]
        public async Task<IActionResult> RegisterCandidate([FromBody] RegisterDto dto)
        {
            try
            {
                var response = await _authService.RegisterCandidateAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // =====================================================
        // REGISTER COMPANY (Tenant request)
        // =====================================================
        [HttpPost("company/register-request")]
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

        // =====================================================
        // LOGIN
        // =====================================================
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
                return Unauthorized(new { Message = ex.Message });
            }
        }

        
        
    }
}