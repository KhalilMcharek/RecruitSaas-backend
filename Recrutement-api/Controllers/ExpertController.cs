
namespace Recrutement_api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Services.Expert;

[ApiController]
[Route("api/tenant")]
//[Authorize(Roles = "Tenant")]
public class ExpertController : ControllerBase
{
    private readonly ExpertService _service;
    public ExpertController(ExpertService service) => _service = service;

    // Lit le TenantId depuis le token JWT du recruteur connecte
   // private Guid GetTenantId() =>
     //   Guid.Parse(User.FindFirstValue("tenantId")
       //     ?? throw new UnauthorizedAccessException("TenantId manquant dans le token."));
     private Guid GetTenantId() =>
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    // POST /api/tenant/experts/invite
    [HttpPost("experts/invite")]
    public async Task<IActionResult> Invite([FromBody] InviteExpertDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _service.InviteExpertAsync(dto, GetTenantId());
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // GET /api/tenant/companies/{companyId}/experts?page=1&search=dupont&isActive=true
    [HttpGet("companies/{companyId}/experts")]
    public async Task<IActionResult> GetByCompany(Guid companyId, [FromQuery] ExpertFilterDto filter)
    {
        var result = await _service.GetExpertsByCompanyAsync(companyId, filter, GetTenantId());
        return Ok(result);
    }

    // GET /api/tenant/experts/{id}
    [HttpGet("experts/{id}")]
    public async Task<IActionResult> GetById(Guid id)
        => Ok(await _service.GetExpertByIdAsync(id, GetTenantId()));

    // PUT /api/tenant/experts/{id}
    [HttpPut("experts/{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpertDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateExpertAsync(id, dto, GetTenantId()));
    }

    // PUT /api/tenant/experts/{id}/deactivate
    [HttpPut("experts/{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _service.DeactivateExpertAsync(id, GetTenantId());
        return Ok(new { message = "Expert desactive." });
    }

    // PUT /api/tenant/experts/{id}/reactivate
    [HttpPut("experts/{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        await _service.ReactivateExpertAsync(id, GetTenantId());
        return Ok(new { message = "Expert reactive." });
    }
    // DELETE /api/tenant/experts/{id}
[HttpDelete("experts/{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    await _service.DeleteExpertAsync(id, GetTenantId());
    return NoContent();
}
}