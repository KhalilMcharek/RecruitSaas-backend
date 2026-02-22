using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Recrutement_api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/admin/logs")]
    [ApiController]
    public class AdminLogsController : ControllerBase
    {
        private readonly string _logPath;
        private readonly EmailService _emailService;

        public AdminLogsController(EmailService emailService)
        {
            _logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            _emailService = emailService;
        }

        //  Liste des fichiers de logs
        [HttpGet]
        public IActionResult GetLogFiles()
        {
            if (!Directory.Exists(_logPath))
                return NotFound("Logs folder not found.");

            var files = Directory.GetFiles(_logPath)
                                 .Select(Path.GetFileName);

            return Ok(files);
        }

        //  Contenu d'un fichier avec pagination
        [HttpGet("{fileName}")]
        public IActionResult GetLogContent(string fileName, int page = 1, int pageSize = 200)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Max(pageSize, 1);

            var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));

            if (!fullPath.StartsWith(_logPath, System.StringComparison.InvariantCultureIgnoreCase))
                return BadRequest("Invalid file path.");

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found.");

            var lines = System.IO.File.ReadLines(fullPath)
                                      .Skip((page - 1) * pageSize)
                                      .Take(pageSize);

            return Ok(lines);
        }

        //  Filtrage par TenantId
        [HttpGet("{fileName}/filter")]
        public IActionResult FilterByTenant(string fileName, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest("TenantId cannot be empty.");

            var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));

            if (!fullPath.StartsWith(_logPath, System.StringComparison.InvariantCultureIgnoreCase))
                return BadRequest("Invalid file path.");

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found.");

            var filtered = System.IO.File.ReadLines(fullPath)
                                         .Where(l => l.Contains($"TenantId\":\"{tenantId}\""));

            return Ok(filtered);
        }

        //  Statistiques et alerte email si erreurs > seuil
        [HttpGet("stats/{fileName}")]
        public async Task<IActionResult> GetStats(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));

            if (!fullPath.StartsWith(_logPath, System.StringComparison.InvariantCultureIgnoreCase))
                return BadRequest("Invalid file path.");

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found.");

            var lines = System.IO.File.ReadLines(fullPath);

            var total = lines.Count();
            var errors = lines.Count(l => l.Contains("Error"));
            var warnings = lines.Count(l => l.Contains("Warning"));

            // Envoi d'alerte email si erreurs > 10
            if (errors > 10)
            {
                await _emailService.SendCriticalAlert(
                    $"Le fichier {fileName} contient {errors} erreurs !"
                );
            }

            return Ok(new
            {
                TotalLogs = total,
                Errors = errors,
                Warnings = warnings
            });
        }
    }
}
