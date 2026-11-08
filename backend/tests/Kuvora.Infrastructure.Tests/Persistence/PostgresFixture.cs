using Kuvora.Application.Common;
using Kuvora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Infrastructure.Tests.Persistence;

/// <summary>
/// Prepara una base PostgreSQL REAL para las pruebas: aplica todas las migraciones una sola vez.
/// La cadena de conexión viene de la variable de entorno KUVORA_TEST_DB (nunca va en el repositorio).
/// </summary>
public sealed class PostgresFixture
{
    public const string EnvironmentVariable = "KUVORA_TEST_DB";

    public DbContextOptions<AppDbContext> Options { get; }

    public PostgresFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Falta la variable de entorno {EnvironmentVariable} con la cadena de conexión de la base de pruebas.");

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        // Aplicar las migraciones también comprueba que se pueden ejecutar desde cero
        using var db = new AppDbContext(Options, new FakeCurrentTenant(null), TimeProvider.System);
        db.Database.Migrate();
    }
}

/// <summary>Restaurante "actual" fijado a mano para los tests (null = petición anónima).</summary>
internal sealed class FakeCurrentTenant(Guid? tenantId) : ICurrentTenant
{
    public Guid? TenantId => tenantId;
}
