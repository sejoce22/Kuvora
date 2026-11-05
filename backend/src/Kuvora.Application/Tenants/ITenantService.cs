namespace Kuvora.Application.Tenants;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);
}