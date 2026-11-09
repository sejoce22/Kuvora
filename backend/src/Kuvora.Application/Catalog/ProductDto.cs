namespace Kuvora.Application.Catalog;

public sealed record ProductDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    long Price,
    bool IsActive);
