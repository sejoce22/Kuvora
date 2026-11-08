using Kuvora.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Tenants;

internal sealed class TenantService(IAppDbContext db) : ITenantService
{
    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TenantDto(t.Id, t.Name, t.IsActive, t.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
