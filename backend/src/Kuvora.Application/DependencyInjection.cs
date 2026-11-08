using Kuvora.Application.Auth;
using Kuvora.Application.Tenants;
using Kuvora.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Kuvora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
