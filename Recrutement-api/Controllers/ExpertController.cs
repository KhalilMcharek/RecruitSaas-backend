namespace Recrutement_api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Services.Expert;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/tenant")]
[Authorize]
public class ExpertController : ControllerBase
{
    private readonly ExpertService _service;

    public ExpertController(ExpertService service) => _service = service;

    private Guid GetTenantId()
    {
        var claims = User.Claims.ToList();
        foreach (var c in claims)
            Console.WriteLine($"[CLAIM] {c.Type} = {c.Value}");

        var tenantClaim = User.FindFirstValue("tenantId");
        if (!string.IsNullOrEmpty(tenantClaim))
            return Guid.Parse(tenantClaim);

        var userIdStr = User.FindFirstValue("sub")
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdStr))
            throw new UnauthorizedAccessException("Token invalide : aucun identifiant trouvé.");

        return Guid.Parse(userIdStr);
    }

    [HttpPost("experts/invite")]
    public async Task<IActionResult> Invite([FromBody] InviteExpertDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _service.InviteExpertAsync(dto, GetTenantId());
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("companies/{companyId}/experts")]
    public async Task<IActionResult> GetByCompany(Guid companyId, [FromQuery] ExpertFilterDto filter)
    {
        var result = await _service.GetExpertsByCompanyAsync(companyId, filter, GetTenantId());
        return Ok(result);
    }

    [HttpGet("experts/{id}")]
    public async Task<IActionResult> GetById(Guid id)
        => Ok(await _service.GetExpertByIdAsync(id, GetTenantId()));

    [HttpPut("experts/{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExpertUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.ExpertUpdateAsync(id, dto, GetTenantId()));
    }

    [HttpPut("experts/{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _service.DeactivateExpertAsync(id, GetTenantId());
        return Ok(new { message = "Expert desactive." });
    }

    [HttpPut("experts/{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        await _service.ReactivateExpertAsync(id, GetTenantId());
        return Ok(new { message = "Expert reactive." });
    }

    [HttpDelete("experts/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteExpertAsync(id, GetTenantId());
        return NoContent();
    }

    [HttpPut("experts/{id}/assign-offre")]
    public async Task<IActionResult> AssignOffre(Guid id, [FromBody] AssignOffreDto dto)
    {
        try
        {
            var result = await _service.AssignOffreAsync(id, dto, GetTenantId());
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("offres-disponibles")]
    public async Task<IActionResult> GetOffresDisponibles(
        [FromServices] Recrutement_api.Data.ApplicationDbContext context)
    {
        var tenantId = GetTenantId();

        Guid resolvedTenantId;
        bool isTenant = await context.Tenants.AnyAsync(t => t.Id == tenantId);
        if (isTenant)
        {
            resolvedTenantId = tenantId;
        }
        else
        {
            var tenant = await context.Tenants
                .Where(t => t.UtilisateurId == tenantId)
                .FirstOrDefaultAsync();
            if (tenant == null) return Unauthorized();
            resolvedTenantId = tenant.Id;
        }

        var offres = await context.OffresEmploi
            .Include(o => o.Entreprise)
            .Where(o => o.Entreprise.TenantId == resolvedTenantId)
            .Select(o => new { o.Id, o.Titre })
            .ToListAsync();

        return Ok(offres);
    }
}