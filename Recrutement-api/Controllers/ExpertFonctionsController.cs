using System;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

[ApiController]
[Route("api/expert")]
[Authorize]
public class ExpertFonctionsController : ControllerBase
{
    private readonly IExpertService _expertService;
    private readonly ICurrentUserService _currentUser;
    private readonly Cloudinary _cloudinary;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExpertFonctionsController> _logger;
    private readonly ApplicationDbContext _context;

    public ExpertFonctionsController(
        IExpertService expertService,
        ICurrentUserService currentUser,
        IOptions<CloudinarySettings> cloudinaryConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<ExpertFonctionsController> logger,
        ApplicationDbContext context)
    {
        _expertService = expertService;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _context = context;

        var account = new Account(
            cloudinaryConfig.Value.CloudName,
            cloudinaryConfig.Value.ApiKey,
            cloudinaryConfig.Value.ApiSecret
        );
        _cloudinary = new Cloudinary(account);
    }

    [HttpGet("{expertId}/offres")]
    public async Task<IActionResult> GetOffresAssignees(Guid expertId)
    {
        try
        {
            var offres = await _expertService.GetOffresAssigneesAsync(expertId);
            return Ok(offres);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

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
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("{expertId}/candidatures/{candidatureId}")]
    public async Task<IActionResult> GetCandidatureDetail(Guid expertId, Guid candidatureId)
    {
        try
        {
            var detail = await _expertService.GetCandidatureDetailAsync(expertId, candidatureId);
            return Ok(detail);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{expertId}/avis")]
    public async Task<IActionResult> SoumettreAvis(Guid expertId, [FromBody] SoumettreAvisDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.Score < 0f || dto.Score > 5f)
            return BadRequest(new { message = "Le score doit être compris entre 0 et 5." });
        try
        {
            var avis = await _expertService.SoumettreAvisAsync(expertId, dto);
            return Ok(avis);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── Notifications ──────────────────────────────────────────────

    [HttpGet("{expertId}/notifications/feed")]
    public async Task<IActionResult> GetNotificationsFeed(Guid expertId)
    {
        var notifs = await _context.Notifications
            .Where(n => n.ExpertId == expertId)
            .OrderByDescending(n => n.CreeLe)
            .Take(30)
            .Select(n => new {
                id = n.Id.ToString(),
                type = n.Type,
                title = n.Title,
                body = n.Body,
                creeLe = n.CreeLe,
                read = n.Read,
                offreId = n.OffreId,
                candidatId = n.CandidatId
            })
            .ToListAsync();

        return Ok(notifs);
    }

    [HttpPatch("{expertId}/notifications/{notifId}/read")]
    public async Task<IActionResult> MarkRead(Guid expertId, Guid notifId)
    {
        var n = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notifId && n.ExpertId == expertId);

        if (n == null) return NotFound();
        n.Read = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Proxy ───────────────────────────────────────────────────────

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
                _logger.LogInformation("PROXY —  Succès [{label}]", label);
                Response.Headers.Append("Cache-Control", "private, max-age=3600");
                Response.Headers.Append("X-Content-Type-Options", "nosniff");
                return File(bytes, GetContentType(url));
            }
        }

        _logger.LogWarning("PROXY —  Échec total pour: {url}", url);
        return StatusCode(502, new { message = "Impossible de récupérer le fichier depuis Cloudinary." });
    }

    [HttpGet("{expertId}/profil")]
    public async Task<IActionResult> GetProfil(Guid expertId)
    {
        try
        {
            var expert = await _expertService.GetProfilAsync(expertId);
            return Ok(expert);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("{expertId}/profil")]
    public async Task<IActionResult> UpdateProfil(Guid expertId, [FromBody] UpdateProfilExpertDto dto)
    {
        try
        {
            var result = await _expertService.UpdateProfilAsync(expertId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static List<(string Url, string Label)> BuildCandidateUrls(string originalUrl)
    {
        var list = new List<(string, string)>();
        list.Add((originalUrl, "original"));
        if (originalUrl.Contains("/image/upload/", StringComparison.OrdinalIgnoreCase))
            list.Add((originalUrl.Replace("/image/upload/", "/raw/upload/", StringComparison.OrdinalIgnoreCase), "raw"));
        if (originalUrl.Contains("/raw/upload/", StringComparison.OrdinalIgnoreCase))
            list.Add((originalUrl.Replace("/raw/upload/", "/image/upload/", StringComparison.OrdinalIgnoreCase), "image"));
        return list;
    }

    private async Task<byte[]?> TryGet(HttpClient client, string url)
    {
        try
        {
            var response = await client.GetAsync(url);
            _logger.LogInformation("    → HTTP {code}", (int)response.StatusCode);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("    → Exception: {msg}", ex.Message);
            return null;
        }
    }

    private static string GetContentType(string url)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(new Uri(url).AbsolutePath).TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "pdf" => "application/pdf",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "doc" => "application/msword",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                "svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }
        catch { return "application/octet-stream"; }
    }
    [HttpGet("tenant/candidatures/{candidatureId}/avis")]
[AllowAnonymous] // ou gardez [Authorize] selon votre auth
public async Task<IActionResult> GetAvisByCandidature(Guid candidatureId)
{
    var avis = await _context.AvisExperts
        .Include(a => a.Expert)
        .Where(a => a.CandidatureId == candidatureId)
        .Select(a => new {
            id          = a.Id,
            score       = a.Score,
            commentaire = a.Commentaire,
            creeLe      = a.CreeLe,
           expertNom   = a.Expert.FirstName + " " + a.Expert.LastName,
expertEmail = a.Expert.Email
        })
        .ToListAsync();

    return Ok(avis);
}
}