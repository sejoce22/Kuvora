using Kuvora.Api.Authorization;
using Kuvora.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace Kuvora.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Seguro por defecto: todo exige usuario autenticado salvo [AllowAnonymous]
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(Policies.AdminOnly, policy =>
                policy.RequireRole(Role(UserRole.Admin)))
            .AddPolicy(Policies.ManageOrders, policy =>
                policy.RequireRole(Role(UserRole.Admin), Role(UserRole.Waiter), Role(UserRole.Cashier)))
            .AddPolicy(Policies.UseKitchen, policy =>
                policy.RequireRole(Role(UserRole.Admin), Role(UserRole.Kitchen)))
            .AddPolicy(Policies.TakePayments, policy =>
                policy.RequireRole(Role(UserRole.Admin), Role(UserRole.Cashier)));

        return services;
    }

    // Los roles del token son el nombre del enum ("Admin"), así no hay textos sueltos
    private static string Role(UserRole role) => role.ToString();
}