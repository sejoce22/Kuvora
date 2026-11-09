using Kuvora.Application.Common;
using Kuvora.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Catalog;

internal sealed class CategoryService(IAppDbContext db, ICurrentUser currentUser) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Sin Where por TenantId: el filtro global ya limita a mi restaurante
        var query = db.Categories.AsNoTracking();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        await EnsureNameIsAvailableAsync(request.Name, Guid.Empty, cancellationToken);

        // La categoría siempre queda en el restaurante de quien la crea
        var category = new Category(currentUser.TenantId, request.Name, request.SortOrder);

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(
        Guid id,
        SaveCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetByIdAsync(id, cancellationToken);

        await EnsureNameIsAvailableAsync(request.Name, id, cancellationToken);

        category.Update(request.Name, request.SortOrder);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var category = await GetByIdAsync(id, cancellationToken);

        if (isActive)
        {
            category.Activate();
        }
        else
        {
            if (await db.Products.AnyAsync(p => p.CategoryId == id && p.IsActive, cancellationToken))
                throw new ConflictException("No puedes desactivar una categoría que tiene productos activos.");

            category.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    // Con seguimiento (sin AsNoTracking): después se modifica y se guarda.
    private async Task<Category> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new NotFoundException("La categoría no existe.");

    // Compara sin mayúsculas y contra TODAS las categorías (también las inactivas),
    // porque el índice único de la base las incluye. excludeId evita chocar consigo misma al editar.
    private async Task EnsureNameIsAvailableAsync(string name, Guid excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();

        var exists = await db.Categories.AnyAsync(
            c => c.Id != excludeId && c.Name.ToLower() == normalizedName,
            cancellationToken);

        if (exists)
            throw new ConflictException("Ya existe una categoría con ese nombre.");
    }

    private static CategoryDto ToDto(Category category) =>
        new(category.Id, category.Name, category.SortOrder, category.IsActive);
}
