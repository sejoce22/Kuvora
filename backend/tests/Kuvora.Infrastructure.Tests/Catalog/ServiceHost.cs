using Kuvora.Application;
using Kuvora.Application.Catalog;
using Kuvora.Application.Common;
using Kuvora.Application.Orders;
using Kuvora.Application.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
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

    private ServiceHost(PostgresFixture fixture, Guid tenantId, Guid userId, UserRole role)
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantId), TimeProvider.System));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser(userId, tenantId, role));

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public ICategoryService Categories => _scope.ServiceProvider.GetRequiredService<ICategoryService>();
    public IProductService Products => _scope.ServiceProvider.GetRequiredService<IProductService>();
    public IDiningTableService Tables => _scope.ServiceProvider.GetRequiredService<IDiningTableService>();
    public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

    // Sin userId se usa uno inventado: sirve para catálogo y mesas. Para pedidos hace falta un usuario REAL
    // (orders.waiter_id es una llave foránea): créalo con CreateUserAsync.
    public static ServiceHost For(
        PostgresFixture fixture,
        Guid tenantId,
        Guid? userId = null,
        UserRole role = UserRole.Admin) =>
        new(fixture, tenantId, userId ?? Guid.CreateVersion7(), role);

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

    // Inserta un usuario real del restaurante (para que el pedido pueda apuntar a él como mesero).
    public static async Task<Guid> CreateUserAsync(
        PostgresFixture fixture,
        Guid tenantId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        await using var db = new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantId), TimeProvider.System);

        var user = new User(tenantId, $"{Guid.NewGuid():N}@test.com", "hash-de-prueba", $"Usuario {role}", role);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

internal sealed class FakeCurrentUser(Guid userId, Guid tenantId, UserRole role = UserRole.Admin) : ICurrentUser
{
    public Guid UserId => userId;
    public Guid TenantId => tenantId;
    public UserRole Role => role;
}
