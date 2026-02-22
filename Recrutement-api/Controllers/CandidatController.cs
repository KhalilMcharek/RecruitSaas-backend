using Microsoft.AspNetCore.Mvc;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CandidatController : ControllerBase
    {
        private readonly ILogger<CandidatController> _logger;

        public CandidatController(ILogger<CandidatController> logger)
        {
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("User accessed Candidat test endpoint");
            _logger.LogError("Simulated critical error in CandidatController");

            return Ok("Test logs working !");
        }
    }
}

