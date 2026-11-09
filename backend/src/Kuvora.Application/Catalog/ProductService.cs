using Kuvora.Application.Common;
using Kuvora.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Catalog;

internal sealed class ProductService(IAppDbContext db, ICurrentUser currentUser) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
        Guid? categoryId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Sin Where por TenantId: el filtro global limita productos y categorías a mi restaurante
        var query =
            from p in db.Products.AsNoTracking()
            join c in db.Categories.AsNoTracking() on p.CategoryId equals c.Id
            select new { Product = p, Category = c };

        if (categoryId is not null)
            query = query.Where(x => x.Product.CategoryId == categoryId);

        if (!includeInactive)
            query = query.Where(x => x.Product.IsActive);

        return await query
            .OrderBy(x => x.Category.SortOrder)
            .ThenBy(x => x.Category.Name)
            .ThenBy(x => x.Product.Name)
            .Select(x => new ProductDto(
                x.Product.Id,
                x.Product.CategoryId,
                x.Category.Name,
                x.Product.Name,
                x.Product.Description,
                x.Product.Price,
                x.Product.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var category = await GetActiveCategoryAsync(request.CategoryId ?? Guid.Empty, cancellationToken);

        await EnsureNameIsAvailableAsync(request.Name, Guid.Empty, cancellationToken);

        // El producto siempre queda en el restaurante de quien lo crea
        var product = new Product(
            currentUser.TenantId,
            category.Id,
            request.Name,
            request.Price,
            request.Description);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product, category.Name);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await GetByIdAsync(id, cancellationToken);
        var category = await GetActiveCategoryAsync(request.CategoryId ?? Guid.Empty, cancellationToken);

        await EnsureNameIsAvailableAsync(request.Name, id, cancellationToken);

        product.Update(category.Id, request.Name, request.Price, request.Description);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product, category.Name);
    }

    public async Task<ProductDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var product = await GetByIdAsync(id, cancellationToken);

        // La categoría de un producto siempre existe (llave foránea); hace falta para el nombre del DTO
        var category = await db.Categories
            .AsNoTracking()
            .FirstAsync(c => c.Id == product.CategoryId, cancellationToken);

        if (isActive)
        {
            if (!category.IsActive)
                throw new ConflictException("No puedes activar un producto cuya categoría está inactiva.");

            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product, category.Name);
    }

    private async Task<Product> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException("El producto no existe.");

    // Una categoría de otro restaurante tampoco se ve: el filtro global la hace "no existir".
    private async Task<Category> GetActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("La categoría no existe.");

        if (!category.IsActive)
            throw new ConflictException("La categoría está inactiva.");

        return category;
    }

    private async Task EnsureNameIsAvailableAsync(string name, Guid excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();

        var exists = await db.Products.AnyAsync(
            p => p.Id != excludeId && p.Name.ToLower() == normalizedName,
            cancellationToken);

        if (exists)
            throw new ConflictException("Ya existe un producto con ese nombre.");
    }

    private static ProductDto ToDto(Product product, string categoryName) =>
        new(
            product.Id,
            product.CategoryId,
            categoryName,
            product.Name,
            product.Description,
            product.Price,
            product.IsActive);
}
