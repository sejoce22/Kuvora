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

    // Cada clase de pruebas crea su propio fixture, y xUnit corre las clases en paralelo. Si todas migraran a la vez
    // una base vacía (como en el CI) chocarían entre sí. El candado es estático para que valga entre fixtures.
    private static readonly Lock MigrationLock = new();
    private static bool _migrated;

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

        // Aplicar las migraciones también comprueba que se pueden ejecutar desde cero. Se hace una sola vez por ejecución.
        lock (MigrationLock)
        {
            if (_migrated) return;

            using var db = new AppDbContext(Options, new FakeCurrentTenant(null), TimeProvider.System);
            db.Database.Migrate();
            _migrated = true;
        }
    }
}

/// <summary>Restaurante "actual" fijado a mano para los tests (null = petición anónima).</summary>
internal sealed class FakeCurrentTenant(Guid? tenantId) : ICurrentTenant
{
    public Guid? TenantId => tenantId;
}
