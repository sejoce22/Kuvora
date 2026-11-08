using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Kuvora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Infrastructure.Tests.Persistence;

/// <summary>
/// Pruebas de aislamiento entre restaurantes contra PostgreSQL real.
/// Cada test crea sus propios restaurantes y usuarios con datos únicos, así que no se pisan entre sí.
/// </summary>
public class TenantIsolationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppDbContext CreateContext(Guid? tenantId, TimeProvider? timeProvider = null) =>
        new(fixture.Options, new FakeCurrentTenant(tenantId), timeProvider ?? TimeProvider.System);

    private static User NewUser(Guid tenantId) =>
        new(tenantId, $"{Guid.NewGuid():N}@test.com", "hash-falso-123", "Usuario de prueba", UserRole.Waiter);

    private sealed record Seed(Guid TenantA, Guid TenantB, Guid UserA, Guid UserB);

    // Dos restaurantes con un usuario cada uno. Se inserta como petición anónima (sin restaurante),
    // igual que hace el registro.
    private async Task<Seed> SeedTwoTenantsAsync()
    {
        await using var db = CreateContext(null);

        var tenantA = new Tenant("Restaurante A");
        var tenantB = new Tenant("Restaurante B");
        var userA = NewUser(tenantA.Id);
        var userB = NewUser(tenantB.Id);

        db.Tenants.AddRange(tenantA, tenantB);
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync(Ct);

        return new Seed(tenantA.Id, tenantB.Id, userA.Id, userB.Id);
    }

    [Fact]
    public async Task Users_AsTenantA_ReturnsOnlyTenantAUsers()
    {
        var seed = await SeedTwoTenantsAsync();
        await using var db = CreateContext(seed.TenantA);

        var users = await db.Users.ToListAsync(Ct);

        Assert.Contains(users, u => u.Id == seed.UserA);
        Assert.DoesNotContain(users, u => u.Id == seed.UserB);
        Assert.All(users, u => Assert.Equal(seed.TenantA, u.TenantId));
    }

    [Fact]
    public async Task Users_LookingUpAnotherTenantsUserById_ReturnsNull()
    {
        var seed = await SeedTwoTenantsAsync();
        await using var db = CreateContext(seed.TenantA);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == seed.UserB, Ct);

        Assert.Null(user);
    }

    [Fact]
    public async Task Users_WithoutTenant_ReturnsNothing()
    {
        await SeedTwoTenantsAsync();
        await using var db = CreateContext(null);

        var users = await db.Users.ToListAsync(Ct);

        // Falla cerrado: una petición sin restaurante no ve ningún dato de restaurantes
        Assert.Empty(users);
    }

    [Fact]
    public async Task Users_IgnoreQueryFilters_ReturnsUsersOfAllTenants()
    {
        var seed = await SeedTwoTenantsAsync();
        await using var db = CreateContext(null);

        var ids = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == seed.UserA || u.Id == seed.UserB)
            .Select(u => u.Id)
            .ToListAsync(Ct);

        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public async Task SaveChanges_UserInOwnTenant_Succeeds()
    {
        var seed = await SeedTwoTenantsAsync();
        await using var db = CreateContext(seed.TenantA);
        var user = NewUser(seed.TenantA);

        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, Ct));
    }

    [Fact]
    public async Task SaveChanges_UserInAnotherTenant_Throws()
    {
        var seed = await SeedTwoTenantsAsync();
        await using var db = CreateContext(seed.TenantA);

        db.Users.Add(NewUser(seed.TenantB));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task SaveChanges_ModifiedEntity_SetsUpdatedAtUtc()
    {
        var seed = await SeedTwoTenantsAsync();
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var tokenHash = $"hash-{Guid.NewGuid():N}";

        await using (var insert = CreateContext(null))
        {
            insert.RefreshTokens.Add(new RefreshToken(seed.UserA, tokenHash, now.AddDays(7)));
            await insert.SaveChangesAsync(Ct);
        }

        await using (var update = CreateContext(null, new FixedTimeProvider(now)))
        {
            var token = await update.RefreshTokens.SingleAsync(r => r.TokenHash == tokenHash, Ct);
            Assert.Null(token.UpdatedAtUtc); // recién creado: nunca se ha modificado

            token.Revoke(now);
            await update.SaveChangesAsync(Ct);
        }

        await using var read = CreateContext(null);
        var saved = await read.RefreshTokens.AsNoTracking().SingleAsync(r => r.TokenHash == tokenHash, Ct);

        Assert.Equal(now, saved.UpdatedAtUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
