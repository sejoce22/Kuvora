namespace Kuvora.Application.Catalog;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(
        Guid? categoryId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
