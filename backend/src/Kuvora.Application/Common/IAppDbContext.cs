using Kuvora.Domain.Catalog;
using Kuvora.Domain.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
namespace Kuvora.Application.Common;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<DiningTable> DiningTables { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
