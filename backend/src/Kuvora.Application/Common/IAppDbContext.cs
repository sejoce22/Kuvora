using Kuvora.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Common;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}