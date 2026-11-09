using Kuvora.Application.Auth;
using Kuvora.Application.Catalog;
using Kuvora.Application.Tables;
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
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDiningTableService, DiningTableService>();

        return services;
    }
}
