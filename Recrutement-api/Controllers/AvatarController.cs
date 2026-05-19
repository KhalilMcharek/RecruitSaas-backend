using Microsoft.AspNetCore.Mvc;

namespace Recrutement_api.Controllers
{
   
    [ApiController]
    [Route("api/[controller]")]
    public class AvatarController : ControllerBase
    {
        // GET /api/avatar/recruteur
        // Retourne : { "url": "https://localhost:7156/avatars/recruteur.glb" }
        [HttpGet("{nom}")]
        public IActionResult GetUrl(string nom)
        {
            var fichier = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "avatars", $"{nom}.glb");

            if (!System.IO.File.Exists(fichier))
                return NotFound(new { message = $"Avatar '{nom}.glb' introuvable dans wwwroot/avatars/" });

            var url = $"{Request.Scheme}://{Request.Host}/avatars/{nom}.glb";
            return Ok(new { url, nom });
        }
    }
}