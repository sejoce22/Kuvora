using Kuvora.Application.Common;
using Kuvora.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}