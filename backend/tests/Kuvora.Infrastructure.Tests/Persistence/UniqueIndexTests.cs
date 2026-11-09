using Kuvora.Domain.Catalog;
using Kuvora.Infrastructure.Persistence;
using Kuvora.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kuvora.Infrastructure.Tests.Persistence;

/// <summary>
/// La red de seguridad de la base: si dos peticiones pasan a la vez la revisión del servicio,
/// el índice único frena a la segunda con el error 23505. La Api lo traduce a 409 (GlobalExceptionHandler).
/// </summary>
public class UniqueIndexTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SavingTwoCategoriesWithTheSameName_FailsWithUniqueViolation()
    {
        var tenantId = await ServiceHost.CreateTenantAsync(fixture, Ct);
        await using var db = new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantId), TimeProvider.System);

        db.Categories.Add(new Category(tenantId, "Postres"));
        db.Categories.Add(new Category(tenantId, "Postres"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [Fact]
    public async Task SameCategoryNameInDifferentTenants_IsAllowed()
    {
        var tenantA = await ServiceHost.CreateTenantAsync(fixture, Ct);
        var tenantB = await ServiceHost.CreateTenantAsync(fixture, Ct);

        await using (var dbA = new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantA), TimeProvider.System))
        {
            dbA.Categories.Add(new Category(tenantA, "Postres"));
            await dbA.SaveChangesAsync(Ct);
        }

        await using var dbB = new AppDbContext(fixture.Options, new FakeCurrentTenant(tenantB), TimeProvider.System);
        dbB.Categories.Add(new Category(tenantB, "Postres"));

        await dbB.SaveChangesAsync(Ct);
    }
}
