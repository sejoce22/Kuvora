using Kuvora.Api.Authorization;
using Kuvora.Application.Common;
using Kuvora.Application.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = Policies.AdminOnly)]
public class TenantsController(ITenantService tenantService, ICurrentUser currentUser) : ControllerBase
{
    // Un restaurante solo puede ver el suyo. Crear restaurantes se hace con /api/auth/register.
    [HttpGet("current")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetByIdAsync(currentUser.TenantId, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
