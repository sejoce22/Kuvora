using Kuvora.Application.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Kuvora.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController(ITenantService tenantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await tenantService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetByIdAsync(id, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create(
        CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }
}