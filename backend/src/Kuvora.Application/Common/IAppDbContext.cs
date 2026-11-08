using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Common;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
