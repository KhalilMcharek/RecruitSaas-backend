using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Controllers
{
    [Route("api/team")]
    [ApiController]
    [Authorize]
    public class TeamController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

   
        public async Task<IActionResult> GetExperts(
            [FromQuery] Guid? entrepriseId,
            [FromQuery] string? search)
        {
            var experts = await _teamService.GetExpertsAsync(entrepriseId, search);
            return Ok(experts);
        }

     
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExpert(Guid id)
        {
            try
            {
                var expert = await _teamService.GetExpertByIdAsync(id);
                return Ok(expert);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

      
        [HttpPost]
        public async Task<IActionResult> CreateExpert([FromBody] ExpertCreateDto dto)
        {
            try
            {
                var expert = await _teamService.CreateExpertAsync(dto);
                return CreatedAtAction(nameof(GetExpert), new { id = expert.Id }, expert);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

      
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpert(Guid id, [FromBody] ExpertUpdateDto dto)
        {
            try
            {
                var expert = await _teamService.UpdateExpertAsync(id, dto);
                return Ok(expert);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpert(Guid id)
        {
            try
            {
                await _teamService.DeleteExpertAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

       
        [HttpGet("entreprises")]
        public async Task<IActionResult> GetEntreprises()
        {
            var entreprises = await _teamService.GetEntreprisesAsync();
            return Ok(entreprises);
        }
    }
}
