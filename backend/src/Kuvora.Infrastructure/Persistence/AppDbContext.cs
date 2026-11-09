using Kuvora.Application.Common;
using Kuvora.Domain.Catalog;
using Kuvora.Domain.Common;
using Kuvora.Domain.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Kuvora.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();

    // EF lee esta propiedad en CADA consulta, con el valor de la petición en curso.
    // Debe ser una propiedad del contexto: así EF la trata como parámetro y no la "congela" en el modelo.
    private Guid? CurrentTenantId => currentTenant.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Todo lo que implemente ITenantEntity recibe el filtro automáticamente,
        // también las entidades que se creen en fases futuras (mesas, productos, pedidos...).
        var applyFilter = typeof(AppDbContext).GetMethod(
            nameof(ApplyTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    // Falla cerrado: sin restaurante en el token (CurrentTenantId == null) no se devuelve nada.
    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity =>
        modelBuilder.Entity<T>().HasQueryFilter(e => CurrentTenantId != null && e.TenantId == CurrentTenantId);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyRules()
    {
        var now = timeProvider.GetUtcNow();
        var tenantId = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Auditoría: UpdatedAtUtc se completa solo al modificar
            if (entry.State == EntityState.Modified
                && entry.Entity is Entity
                && entry.Metadata.FindProperty(nameof(Entity.UpdatedAtUtc)) is not null)
            {
                entry.Property(nameof(Entity.UpdatedAtUtc)).CurrentValue = now;
            }

            // Última barrera: un usuario autenticado nunca puede escribir datos de otro restaurante
            if (tenantId is not null
                && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity is ITenantEntity tenantEntity
                && tenantEntity.TenantId != tenantId)
            {
                throw new InvalidOperationException(
                    "Se intentó guardar una entidad de otro restaurante.");
            }
        }
    }
}
