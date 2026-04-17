using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.Services.TenantServices;

namespace Recrutement_api.Controllers
{
    [Route("api/reports")]
    [ApiController]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly ReportService _reportService;

        public ReportsController(ReportService reportService)
        {
            _reportService = reportService;
        }

        // ── GET /api/reports/global/kpis ────────────────────────────
        [HttpGet("global/kpis")]
        public async Task<IActionResult> GetGlobalKpis()
        {
            var data = await _reportService.GetGlobalKpisAsync();
            return Ok(data);
        }

        // ── GET /api/reports/job/{jobId}/candidates/excel ────────────
        [HttpGet("job/{jobId:guid}/candidates/excel")]
        public async Task<IActionResult> GetOfferCandidatesExcel(Guid jobId)
        {
            try
            {
                var bytes = await _reportService.GenerateOfferCandidatesExcelAsync(jobId);
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"candidates_{jobId:N}.xlsx"
                );
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── GET /api/reports/candidate/{id}/pdf ─────────────────────
        [HttpGet("candidate/{id:guid}/pdf")]
        public async Task<IActionResult> GetCandidatePdf(Guid id)
        {
            try
            {
                var bytes = await _reportService.GenerateCandidatePdfAsync(id);
                return File(bytes, "application/pdf", $"candidate_{id:N}.pdf");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── GET /api/reports/candidate/{id}/word ────────────────────
        [HttpGet("candidate/{id:guid}/word")]
        public async Task<IActionResult> GetCandidateWord(Guid id)
        {
            try
            {
                var bytes = await _reportService.GenerateCandidateWordAsync(id);
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"candidate_{id:N}.docx"
                );
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
