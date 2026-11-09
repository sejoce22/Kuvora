namespace Kuvora.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, int SortOrder, bool IsActive);
