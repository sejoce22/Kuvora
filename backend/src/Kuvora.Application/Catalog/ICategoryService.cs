namespace Kuvora.Application.Catalog;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
