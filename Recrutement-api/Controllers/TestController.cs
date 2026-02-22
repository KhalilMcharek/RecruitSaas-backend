using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Recrutement_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        // Endpoint protégé par JWT
        [Authorize]  // ← Ici
        [HttpGet("secure")]
        public IActionResult SecureEndpoint()
        {
            return Ok("Accès autorisé !");
        }

        // Endpoint public (non protégé)
        [HttpGet("public")]
        public IActionResult PublicEndpoint()
        {
            return Ok("Accès public !");
        }
    }
}
