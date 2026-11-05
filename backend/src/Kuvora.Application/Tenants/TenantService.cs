using Kuvora.Application.Common;
using Kuvora.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Tenants;

internal sealed class TenantService(IAppDbContext db) : ITenantService
{
    public async Task<IReadOnlyList<TenantDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Name, t.IsActive, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TenantDto(t.Id, t.Name, t.IsActive, t.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant(request.Name);

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.IsActive, tenant.CreatedAtUtc);
    }
}