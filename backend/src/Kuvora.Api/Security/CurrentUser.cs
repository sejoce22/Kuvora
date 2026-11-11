using System.Security.Claims;
using Kuvora.Application.Common;
using Kuvora.Domain.Users;

namespace Kuvora.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId => GetGuidClaim("sub");

    public Guid TenantId => GetGuidClaim("tenant_id");

    public UserRole Role => Enum.TryParse<UserRole>(GetClaim("role"), out var role)
        ? role
        : throw new InvalidOperationException("El token no tiene un rol válido.");

    private Guid GetGuidClaim(string claimType)
    {
        var value = GetClaim(claimType);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"El token no tiene el claim '{claimType}'.");
    }

    private string? GetClaim(string claimType)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

        return principal.FindFirstValue(claimType);
    }
}
