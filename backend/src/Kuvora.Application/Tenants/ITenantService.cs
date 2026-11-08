namespace Kuvora.Application.Tenants;

public interface ITenantService
{
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
