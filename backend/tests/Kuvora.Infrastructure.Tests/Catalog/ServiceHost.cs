using Kuvora.Application;
using Kuvora.Application.Catalog;
using Kuvora.Application.Common;
using Kuvora.Application.Orders;
using Kuvora.Application.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Infrastructure.Persistence;
using Kuvora.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kuvora.Infrastructure.Tests.Catalog;

/// <summary>
/// Simula UNA petición de un restaurante: arma el contenedor de dependencias con la base real y un
/// "usuario actual" fijo, y entrega los servicios de Application (que son internal) por su interfaz.
/// </summary>
internal sealed class ServiceHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private ServiceHost(PostgresFixture fixture, Guid tenantId)
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantId), TimeProvider.System));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser(Guid.CreateVersion7(), tenantId));

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public ICategoryService Categories => _scope.ServiceProvider.GetRequiredService<ICategoryService>();
    public IProductService Products => _scope.ServiceProvider.GetRequiredService<IProductService>();
    public IDiningTableService Tables => _scope.ServiceProvider.GetRequiredService<IDiningTableService>();
    public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

    public static ServiceHost For(PostgresFixture fixture, Guid tenantId) => new(fixture, tenantId);

    // Inserta un restaurante nuevo (como petición anónima, igual que el registro). Cada test usa los suyos,
    // así los datos de un test nunca chocan con los de otro.
    public static async Task<Guid> CreateTenantAsync(PostgresFixture fixture, CancellationToken cancellationToken)
    {
        await using var db = new AppDbContext(fixture.Options, new FakeCurrentTenant(null), TimeProvider.System);

        var tenant = new Tenant($"Restaurante {Guid.NewGuid():N}");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

internal sealed class FakeCurrentUser(Guid userId, Guid tenantId) : ICurrentUser
{
    public Guid UserId => userId;
    public Guid TenantId => tenantId;
}
