using System;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Controllers
{
    [Route("api/expert")]
    [ApiController]
    [Authorize]
    public class ExpertController : ControllerBase
    {
        private readonly IExpertService _expertService;
        private readonly ICurrentUserService _currentUser;
        private readonly Cloudinary _cloudinary;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExpertController> _logger;

        public ExpertController(
            IExpertService expertService,
            ICurrentUserService currentUser,
            IOptions<CloudinarySettings> cloudinaryConfig,
            IHttpClientFactory httpClientFactory,
            ILogger<ExpertController> logger)
        {
            _expertService     = expertService;
            _currentUser       = currentUser;
            _httpClientFactory = httpClientFactory;
            _logger            = logger;

            var account = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(account);
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/expert/{expertId}/offres
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{expertId}/offres")]
        public async Task<IActionResult> GetOffresAssignees(Guid expertId)
        {
            try
            {
                var offres = await _expertService.GetOffresAssigneesAsync(expertId);
                return Ok(offres);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/expert/{expertId}/candidatures?offreId=&statut=
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{expertId}/candidatures")]
        public async Task<IActionResult> GetCandidatures(
            Guid expertId,
            [FromQuery] Guid? offreId = null,
            [FromQuery] string? statut = null)
        {
            try
            {
                var candidatures = await _expertService.GetCandidaturesAsync(expertId, offreId, statut);
                return Ok(candidatures);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/expert/{expertId}/candidatures/{candidatureId}
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{expertId}/candidatures/{candidatureId}")]
        public async Task<IActionResult> GetCandidatureDetail(Guid expertId, Guid candidatureId)
        {
            try
            {
                var detail = await _expertService.GetCandidatureDetailAsync(expertId, candidatureId);
                return Ok(detail);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/expert/{expertId}/avis
        // ─────────────────────────────────────────────────────────────
        [HttpPost("{expertId}/avis")]
        public async Task<IActionResult> SoumettreAvis(Guid expertId, [FromBody] SoumettreAvisDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.Score < 0f || dto.Score > 5f)
                return BadRequest(new { message = "Le score doit être compris entre 0 et 5." });

            try
            {
                var avis = await _expertService.SoumettreAvisAsync(expertId, dto);
                return Ok(avis);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        }

        // ═════════════════════════════════════════════════════════════
        // GET api/expert/proxy?url=https://res.cloudinary.com/...
        //
        // CAUSE DU BUG IDENTIFIÉE :
        //   Les PDFs sont uploadés via RawUploadParams (resource_type=raw)
        //   → leur vraie URL Cloudinary contient /raw/upload/
        //   Mais si la base de données a stocké /image/upload/ à la place,
        //   Cloudinary retourne 401 car le resource_type ne correspond pas.
        //
        // SOLUTION : essayer les 3 variantes de resource_type dans l'ordre.
        // ═════════════════════════════════════════════════════════════
        [HttpGet("proxy")]
        public async Task<IActionResult> ProxyFichier([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "URL manquante." });

            if (!url.StartsWith("https://res.cloudinary.com/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "URL non autorisée." });

            _logger.LogInformation("PROXY — URL reçue: {url}", url);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            foreach (var (candidateUrl, label) in BuildCandidateUrls(url))
            {
                _logger.LogInformation("PROXY — Essai [{label}]: {url}", label, candidateUrl);
                var bytes = await TryGet(client, candidateUrl);
                if (bytes != null)
                {
                    _logger.LogInformation("PROXY — ✅ Succès [{label}]", label);
                    Response.Headers.Append("Cache-Control", "private, max-age=3600");
                    Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    return File(bytes, GetContentType(url));
                }
            }

            _logger.LogWarning("PROXY — ❌ Échec total pour: {url}", url);
            return StatusCode(502, new { message = "Impossible de récupérer le fichier depuis Cloudinary." });
        }

        // ─────────────────────────────────────────────────────────────
        // Génère toutes les variantes d'URL à essayer
        //
        // Ordre pour les PDFs :
        //   1. URL originale           ex: /image/upload/v.../cvs/file.pdf
        //   2. Variante /raw/upload/   ex: /raw/upload/v.../cvs/file.pdf   ← PDF stocké en raw
        //   3. Variante /image/upload/ ex: /image/upload/v.../cvs/file.pdf ← fallback
        // ─────────────────────────────────────────────────────────────
        private static List<(string Url, string Label)> BuildCandidateUrls(string originalUrl)
        {
            var list = new List<(string, string)>();

            // 1. URL originale telle quelle
            list.Add((originalUrl, "original"));

            // 2. Variante /raw/upload/ si l'URL contient /image/upload/
            if (originalUrl.Contains("/image/upload/", StringComparison.OrdinalIgnoreCase))
            {
                var rawUrl = originalUrl.Replace(
                    "/image/upload/", "/raw/upload/",
                    StringComparison.OrdinalIgnoreCase);
                list.Add((rawUrl, "raw"));
            }

            // 3. Variante /image/upload/ si l'URL contient /raw/upload/
            if (originalUrl.Contains("/raw/upload/", StringComparison.OrdinalIgnoreCase))
            {
                var imageUrl = originalUrl.Replace(
                    "/raw/upload/", "/image/upload/",
                    StringComparison.OrdinalIgnoreCase);
                list.Add((imageUrl, "image"));
            }

            return list;
        }

        // ─────────────────────────────────────────────────────────────
        // HTTP GET — retourne null si échec (4xx/5xx/exception)
        // ─────────────────────────────────────────────────────────────
        private async Task<byte[]?> TryGet(HttpClient client, string url)
        {
            try
            {
                var response = await client.GetAsync(url);
                var code     = (int)response.StatusCode;
                _logger.LogInformation("    → HTTP {code}", code);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync();

                var body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                    _logger.LogWarning("    → Body: {body}", body);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("    → Exception: {msg}", ex.Message);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Content-Type depuis l'extension de l'URL
        // ─────────────────────────────────────────────────────────────
        private static string GetContentType(string url)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(new Uri(url).AbsolutePath)
                              .TrimStart('.').ToLowerInvariant();
                return ext switch
                {
                    "pdf"           => "application/pdf",
                    "docx"          => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "doc"           => "application/msword",
                    "png"           => "image/png",
                    "jpg" or "jpeg" => "image/jpeg",
                    "gif"           => "image/gif",
                    "webp"          => "image/webp",
                    "svg"           => "image/svg+xml",
                    _               => "application/octet-stream"
                };
            }
            catch { return "application/octet-stream"; }
        }
    }
}