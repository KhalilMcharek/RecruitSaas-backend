using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Recrutement_api.Controllers
{
    [Route("api/admin/logs")]
    [ApiController]
    public class AdminLogsController : ControllerBase
    {
        private readonly string _logPath;

        public AdminLogsController(IWebHostEnvironment env)
        {
            // Ajouter Path.DirectorySeparatorChar pour éviter le bug du StartsWith check
            _logPath = Path.Combine(env.ContentRootPath, "logs") + Path.DirectorySeparatorChar;
            
            // Créer le dossier s'il n'existe pas encore
            if (!Directory.Exists(_logPath))
                Directory.CreateDirectory(_logPath);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult GetLogFiles()
        {
            try
            {
                var files = Directory.GetFiles(_logPath)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .OrderByDescending(f => f)
                    .ToList();

                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("{fileName}")]
        public IActionResult GetLogContent(string fileName, int page = 1, int pageSize = 200)
        {
            try
            {
                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));

                // Maintenant StartsWith fonctionne correctement avec le trailing separator
                if (!fullPath.StartsWith(_logPath, StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Invalid file path.");

                if (!System.IO.File.Exists(fullPath))
                    return NotFound($"File not found: {fileName}");

                var lines = System.IO.File.ReadLines(fullPath)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(lines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("{fileName}/filter")]
        public IActionResult FilterByTenant(string fileName, [FromQuery] string tenantId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tenantId))
                    return BadRequest("TenantId cannot be empty.");

                var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));

                if (!fullPath.StartsWith(_logPath, StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Invalid file path.");

                if (!System.IO.File.Exists(fullPath))
                    return NotFound($"File not found: {fileName}");

                var filtered = System.IO.File.ReadLines(fullPath)
                    .Where(l => l.Contains(tenantId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return Ok(filtered);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ← Endpoint de diagnostic temporaire, à supprimer après confirmation
        [HttpGet("debug")]
        public IActionResult Debug()
        {
            return Ok(new {
                LogPath = _logPath,
                Exists = Directory.Exists(_logPath),
                Files = Directory.Exists(_logPath)
                    ? Directory.GetFiles(_logPath).Select(Path.GetFileName)
                    : Array.Empty<string>()
            });
        }
    }
}