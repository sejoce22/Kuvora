using Kuvora.Domain.Catalog;
using Kuvora.Domain.Orders;
using Kuvora.Domain.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Kuvora.Application.Common;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<DiningTable> DiningTables { get; }
    DbSet<Order> Orders { get; }

    // Transacciones y SQL directo (el contador de pedidos). AppDbContext ya lo tiene por heredar de DbContext.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
