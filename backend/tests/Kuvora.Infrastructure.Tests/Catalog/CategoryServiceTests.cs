using Kuvora.Application.Catalog;
using Kuvora.Application.Common;
using Kuvora.Infrastructure.Tests.Persistence;

namespace Kuvora.Infrastructure.Tests.Catalog;

/// <summary>Reglas del servicio de categorías contra PostgreSQL real.</summary>
public class CategoryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SaveCategoryRequest Request(string name, int sortOrder = 0) =>
        new() { Name = name, SortOrder = sortOrder };

    private Task<Guid> NewTenantAsync() => ServiceHost.CreateTenantAsync(fixture, Ct);

    [Fact]
    public async Task Create_ThenGetAll_ReturnsTheCategory()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());

        var created = await host.Categories.CreateAsync(Request("Bebidas", 1), Ct);
        var all = await host.Categories.GetAllAsync(includeInactive: false, Ct);

        Assert.Equal("Bebidas", created.Name);
        Assert.True(created.IsActive);
        Assert.Single(all, c => c.Id == created.Id);
    }

    [Fact]
    public async Task Create_SameNameWithDifferentCase_ThrowsConflict()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        await host.Categories.CreateAsync(Request("Bebidas"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Categories.CreateAsync(Request("bEbIdAs"), Ct));
    }

    [Fact]
    public async Task Create_NameOfAnInactiveCategory_ThrowsConflict()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Categories.CreateAsync(Request("Postres"), Ct);
        await host.Categories.SetActiveAsync(created.Id, false, Ct);

        // El índice único de la base también cuenta las inactivas, así que el servicio las revisa igual
        await Assert.ThrowsAsync<ConflictException>(() => host.Categories.CreateAsync(Request("postres"), Ct));
    }

    [Fact]
    public async Task Create_SameNameInAnotherTenant_Succeeds()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());

        await hostA.Categories.CreateAsync(Request("Bebidas"), Ct);
        var inB = await hostB.Categories.CreateAsync(Request("Bebidas"), Ct);

        Assert.Equal("Bebidas", inB.Name);
    }

    [Fact]
    public async Task GetAll_AnotherTenant_DoesNotSeeMyCategories()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());
        var inA = await hostA.Categories.CreateAsync(Request("Bebidas"), Ct);

        var seenByB = await hostB.Categories.GetAllAsync(includeInactive: true, Ct);

        Assert.DoesNotContain(seenByB, c => c.Id == inA.Id);
    }

    [Fact]
    public async Task Update_CategoryOfAnotherTenant_ThrowsNotFound()
    {
        using var hostA = ServiceHost.For(fixture, await NewTenantAsync());
        using var hostB = ServiceHost.For(fixture, await NewTenantAsync());
        var inA = await hostA.Categories.CreateAsync(Request("Bebidas"), Ct);

        // 404 y no 403: no se revela que el id existe en otro restaurante
        await Assert.ThrowsAsync<NotFoundException>(() => hostB.Categories.UpdateAsync(inA.Id, Request("Hackeada"), Ct));
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_Succeeds()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Categories.CreateAsync(Request("Bebidas", 1), Ct);

        var updated = await host.Categories.UpdateAsync(created.Id, Request("Bebidas", 5), Ct);

        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task Update_NameOfAnotherCategory_ThrowsConflict()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        await host.Categories.CreateAsync(Request("Bebidas"), Ct);
        var postres = await host.Categories.CreateAsync(Request("Postres"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Categories.UpdateAsync(postres.Id, Request("bebidas"), Ct));
    }

    [Fact]
    public async Task Deactivate_WithActiveProducts_ThrowsConflict_AndWorksOnceTheyAreInactive()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var category = await host.Categories.CreateAsync(Request("Bebidas"), Ct);
        var product = await host.Products.CreateAsync(
            new SaveProductRequest { CategoryId = category.Id, Name = "Jugo", Price = 3500 }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Categories.SetActiveAsync(category.Id, false, Ct));

        await host.Products.SetActiveAsync(product.Id, false, Ct);
        var deactivated = await host.Categories.SetActiveAsync(category.Id, false, Ct);

        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task GetAll_HidesInactiveUnlessAsked()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Categories.CreateAsync(Request("Bebidas"), Ct);
        await host.Categories.SetActiveAsync(created.Id, false, Ct);

        var onlyActive = await host.Categories.GetAllAsync(includeInactive: false, Ct);
        var withInactive = await host.Categories.GetAllAsync(includeInactive: true, Ct);

        Assert.Empty(onlyActive);
        Assert.Single(withInactive);
    }

    [Fact]
    public async Task SetActive_IsIdempotent()
    {
        using var host = ServiceHost.For(fixture, await NewTenantAsync());
        var created = await host.Categories.CreateAsync(Request("Bebidas"), Ct);

        var again = await host.Categories.SetActiveAsync(created.Id, true, Ct);

        Assert.True(again.IsActive);
    }
}
