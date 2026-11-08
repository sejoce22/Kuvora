using System.Security.Claims;
using Kuvora.Application.Common;

namespace Kuvora.Api.Security;

internal sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public Guid? TenantId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"), out var id)
            ? id
            : null;
}
