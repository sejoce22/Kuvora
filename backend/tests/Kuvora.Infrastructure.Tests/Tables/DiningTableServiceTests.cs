using Kuvora.Application.Common;
using Kuvora.Application.Tables;
using Kuvora.Infrastructure.Tests.Catalog;
using Kuvora.Infrastructure.Tests.Persistence;

namespace Kuvora.Infrastructure.Tests.Tables;

/// <summary>Reglas del servicio de mesas contra PostgreSQL real.</summary>
public class DiningTableServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SaveDiningTableRequest Request(string name, int capacity = 4, string? zone = null) =>
        new() { Name = name, Capacity = capacity, Zone = zone };

    private Task<Guid> NewTenantAsync() => ServiceHost.CreateTenantAsync(fixture, Ct);

    [Fact]
    public async Task Create_ThenGetAll_ReturnsTheTable()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());

        var created = await host.Tables.CreateAsync(Request("Mesa 1", 4, "Terraza"), Ct);
        var all = await host.Tables.GetAllAsync(includeInactive: false, Ct);

        Assert.Equal("Terraza", created.Zone);
        Assert.Single(all, t => t.Id == created.Id);
    }

    [Fact]
    public async Task Create_SameNameWithDifferentCase_ThrowsConflict()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        await host.Tables.CreateAsync(Request("Mesa 1"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Tables.CreateAsync(Request("mesa 1"), Ct));
    }

    [Fact]
    public async Task Create_SameNameInAnotherTenant_Succeeds()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());
        await hostA.Tables.CreateAsync(Request("Mesa 1"), Ct);

        var inB = await hostB.Tables.CreateAsync(Request("Mesa 1"), Ct);

        Assert.Equal("Mesa 1", inB.Name);
    }

    [Fact]
    public async Task GetAll_AnotherTenant_DoesNotSeeMyTables()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());
        var inA = await hostA.Tables.CreateAsync(Request("Mesa 1"), Ct);

        var seenByB = await hostB.Tables.GetAllAsync(includeInactive: true, Ct);

        Assert.DoesNotContain(seenByB, t => t.Id == inA.Id);
    }

    [Fact]
    public async Task Update_TableOfAnotherTenant_ThrowsNotFound()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());
        var inA = await hostA.Tables.CreateAsync(Request("Mesa 1"), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => hostB.Tables.UpdateAsync(inA.Id, Request("Hackeada"), Ct));
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_Succeeds()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Tables.CreateAsync(Request("Mesa 1", 4), Ct);

        var updated = await host.Tables.UpdateAsync(created.Id, Request("Mesa 1", 6, "Salón"), Ct);

        Assert.Equal(6, updated.Capacity);
        Assert.Equal("Salón", updated.Zone);
    }

    [Fact]
    public async Task Update_NameOfAnotherTable_ThrowsConflict()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        await host.Tables.CreateAsync(Request("Mesa 1"), Ct);
        var second = await host.Tables.CreateAsync(Request("Mesa 2"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Tables.UpdateAsync(second.Id, Request("MESA 1"), Ct));
    }

    [Fact]
    public async Task GetAll_HidesInactiveUnlessAsked()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Tables.CreateAsync(Request("Mesa 1"), Ct);
        await host.Tables.SetActiveAsync(created.Id, false, Ct);

        var onlyActive = await host.Tables.GetAllAsync(includeInactive: false, Ct);
        var withInactive = await host.Tables.GetAllAsync(includeInactive: true, Ct);

        Assert.Empty(onlyActive);
        Assert.Single(withInactive);
    }

    [Fact]
    public async Task GetAll_GroupsByZoneThenName()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        await host.Tables.CreateAsync(Request("Mesa B", zone: "Terraza"), Ct);
        await host.Tables.CreateAsync(Request("Mesa A", zone: "Terraza"), Ct);
        await host.Tables.CreateAsync(Request("Mesa C", zone: "Interior"), Ct);

        var all = await host.Tables.GetAllAsync(includeInactive: false, Ct);

        Assert.Equal(["Mesa C", "Mesa A", "Mesa B"], all.Select(t => t.Name));
    }
}
