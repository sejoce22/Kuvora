using Kuvora.Application.Catalog;
using Kuvora.Application.Common;
using Kuvora.Infrastructure.Tests.Persistence;

namespace Kuvora.Infrastructure.Tests.Catalog;

/// <summary>Reglas del servicio de productos contra PostgreSQL real.</summary>
public class ProductServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SaveProductRequest Request(Guid categoryId, string name, long price = 1000, string? description = null) =>
        new() { CategoryId = categoryId, Name = name, Price = price, Description = description };

    private async Task<(ServiceHost Host, CategoryDto Category)> NewRestaurantWithCategoryAsync(string categoryName = "Bebidas")
    {
        var host = ServiceHost.For(fixture, await ServiceHost.CreateTenantAsync(fixture, Ct));
        var category = await host.Categories.CreateAsync(new SaveCategoryRequest { Name = categoryName }, Ct);
        return (host, category);
    }

    [Fact]
    public async Task Create_ReturnsDtoWithCategoryName()
    {
        var (host, category) = await NewRestaurantWithCategoryAsync();
        using var _ = host;

        var product = await host.Products.CreateAsync(Request(category.Id, "Jugo", 3500, "Natural"), Ct);

        Assert.Equal(category.Id, product.CategoryId);
        Assert.Equal("Bebidas", product.CategoryName);
        Assert.Equal(3500, product.Price);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task Create_SameNameWithDifferentCase_ThrowsConflict()
    {
        var (host, category) = await NewRestaurantWithCategoryAsync();
        using var _ = host;
        await host.Products.CreateAsync(Request(category.Id, "Jugo"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Products.CreateAsync(Request(category.Id, "JUGO"), Ct));
    }

    [Fact]
    public async Task Create_CategoryThatDoesNotExist_ThrowsNotFound()
    {
        var (host, _) = await NewRestaurantWithCategoryAsync();
        using var _ = host;

        await Assert.ThrowsAsync<NotFoundException>(() => host.Products.CreateAsync(Request(Guid.NewGuid(), "Jugo"), Ct));
    }

    [Fact]
    public async Task Create_CategoryOfAnotherTenant_ThrowsNotFound()
    {
        var (hostA, categoryA) = await NewRestaurantWithCategoryAsync();
        using var _a = hostA;
        using var hostB = ServiceHost.For(fixture, await ServiceHost.CreateTenantAsync(fixture, Ct));

        await Assert.ThrowsAsync<NotFoundException>(() => hostB.Products.CreateAsync(Request(categoryA.Id, "Colado"), Ct));
    }

    [Fact]
    public async Task Create_InAnInactiveCategory_ThrowsConflict()
    {
        var (host, category) = await NewRestaurantWithCategoryAsync();
        using var _ = host;
        await host.Categories.SetActiveAsync(category.Id, false, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Products.CreateAsync(Request(category.Id, "Jugo"), Ct));
    }

    [Fact]
    public async Task Activate_WhenItsCategoryIsInactive_ThrowsConflict()
    {
        var (host, category) = await NewRestaurantWithCategoryAsync();
        using var _ = host;
        var product = await host.Products.CreateAsync(Request(category.Id, "Jugo"), Ct);
        await host.Products.SetActiveAsync(product.Id, false, Ct);
        await host.Categories.SetActiveAsync(category.Id, false, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => host.Products.SetActiveAsync(product.Id, true, Ct));
    }

    [Fact]
    public async Task Update_MovesTheProductToAnotherCategory()
    {
        var (host, bebidas) = await NewRestaurantWithCategoryAsync();
        using var _ = host;
        var postres = await host.Categories.CreateAsync(new SaveCategoryRequest { Name = "Postres" }, Ct);
        var product = await host.Products.CreateAsync(Request(bebidas.Id, "Helado"), Ct);

        var updated = await host.Products.UpdateAsync(product.Id, Request(postres.Id, "Helado", 2500), Ct);

        Assert.Equal(postres.Id, updated.CategoryId);
        Assert.Equal("Postres", updated.CategoryName);
        Assert.Equal(2500, updated.Price);
    }

    [Fact]
    public async Task Update_ProductOfAnotherTenant_ThrowsNotFound()
    {
        var (hostA, categoryA) = await NewRestaurantWithCategoryAsync();
        using var _a = hostA;
        var productA = await hostA.Products.CreateAsync(Request(categoryA.Id, "Jugo"), Ct);

        var (hostB, categoryB) = await NewRestaurantWithCategoryAsync();
        using var _b = hostB;

        await Assert.ThrowsAsync<NotFoundException>(() => hostB.Products.UpdateAsync(productA.Id, Request(categoryB.Id, "Hackeado"), Ct));
    }

    [Fact]
    public async Task GetAll_FiltersByCategory_AndHidesInactiveUnlessAsked()
    {
        var (host, bebidas) = await NewRestaurantWithCategoryAsync();
        using var _ = host;
        var postres = await host.Categories.CreateAsync(new SaveCategoryRequest { Name = "Postres" }, Ct);
        var jugo = await host.Products.CreateAsync(Request(bebidas.Id, "Jugo"), Ct);
        var agua = await host.Products.CreateAsync(Request(bebidas.Id, "Agua"), Ct);
        await host.Products.CreateAsync(Request(postres.Id, "Helado"), Ct);
        await host.Products.SetActiveAsync(agua.Id, false, Ct);

        var onlyBebidas = await host.Products.GetAllAsync(bebidas.Id, includeInactive: false, Ct);
        var bebidasWithInactive = await host.Products.GetAllAsync(bebidas.Id, includeInactive: true, Ct);

        Assert.Equal([jugo.Id], onlyBebidas.Select(p => p.Id));
        Assert.Equal(2, bebidasWithInactive.Count);
    }

    [Fact]
    public async Task GetAll_AnotherTenant_DoesNotSeeMyProducts()
    {
        var (hostA, categoryA) = await NewRestaurantWithCategoryAsync();
        using var _a = hostA;
        var productA = await hostA.Products.CreateAsync(Request(categoryA.Id, "Jugo"), Ct);
        using var hostB = ServiceHost.For(fixture, await ServiceHost.CreateTenantAsync(fixture, Ct));

        var seenByB = await hostB.Products.GetAllAsync(categoryId: null, includeInactive: true, Ct);

        Assert.DoesNotContain(seenByB, p => p.Id == productA.Id);
    }
}
