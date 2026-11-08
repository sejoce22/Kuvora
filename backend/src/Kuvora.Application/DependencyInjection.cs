using Kuvora.Application.Auth;
using Kuvora.Application.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Kuvora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}