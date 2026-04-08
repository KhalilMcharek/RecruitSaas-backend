namespace Recrutement_api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Services.Expert;

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
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpertDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateExpertAsync(id, dto, GetTenantId()));
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
}