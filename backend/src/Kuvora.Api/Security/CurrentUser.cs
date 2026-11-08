using System.Security.Claims;
using Kuvora.Application.Common;

namespace Kuvora.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId => GetGuidClaim("sub");

    public Guid TenantId => GetGuidClaim("tenant_id");

    private Guid GetGuidClaim(string claimType)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

        var value = principal.FindFirstValue(claimType);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"El token no tiene el claim '{claimType}'.");
    }
}
