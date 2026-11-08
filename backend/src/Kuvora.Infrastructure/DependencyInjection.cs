using Kuvora.Application.Auth;
using Kuvora.Application.Common;
using Kuvora.Domain.Users;
using Kuvora.Infrastructure.Persistence;
using Kuvora.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kuvora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordService, PasswordService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        return services;
    }
}
