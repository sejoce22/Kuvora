namespace Kuvora.Application.Tables;

public interface IDiningTableService
{
    Task<IReadOnlyList<DiningTableDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<DiningTableDto> CreateAsync(SaveDiningTableRequest request, CancellationToken cancellationToken);
    Task<DiningTableDto> UpdateAsync(Guid id, SaveDiningTableRequest request, CancellationToken cancellationToken);
    Task<DiningTableDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
