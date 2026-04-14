using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Recrutement_api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/admin/logs")]
    [ApiController]
    public class AdminLogsController : ControllerBase
    {
        private readonly string _logPath;

        public AdminLogsController(IWebHostEnvironment env)
        {
            _logPath = Path.Combine(env.ContentRootPath, "logs") + Path.DirectorySeparatorChar;
            if (!Directory.Exists(_logPath))
                Directory.CreateDirectory(_logPath);
        }

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
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet("{fileName}")]
        public IActionResult GetLogContent(string fileName, int page = 1, int pageSize = 200)
        {
            try
            {
                page     = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 1000);
                var fullPath = Path.GetFullPath(Path.Combine(_logPath, fileName));
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
            catch (Exception ex) { return StatusCode(500, ex.Message); }
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
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}