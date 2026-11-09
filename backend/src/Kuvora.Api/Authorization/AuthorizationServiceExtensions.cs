using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Kuvora.Api.Authorization;

public static class AuthorizationServiceExtensions
{
    // ¿Quien hace la petición es Admin? Reutiliza la política AdminOnly en lugar de repetir la lista de roles.
    public static async Task<bool> IsAdminAsync(this IAuthorizationService authorization, ClaimsPrincipal user) =>
        (await authorization.AuthorizeAsync(user, Policies.AdminOnly)).Succeeded;
}
