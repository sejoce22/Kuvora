using Kuvora.Domain.Orders;
using Kuvora.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kuvora.Infrastructure.Persistence.Configurations;

public class OrderCounterConfiguration : IEntityTypeConfiguration<OrderCounter>
{
    public void Configure(EntityTypeBuilder<OrderCounter> builder)
    {
        builder.ToTable("order_counters");

        // La clave es el propio restaurante: una sola fila por tenant.
        // Esa clave primaria es la que hace posible el INSERT ... ON CONFLICT (tenant_id).
        builder.HasKey(c => c.TenantId);
        builder.Property(c => c.TenantId).ValueGeneratedNever();

        builder.Property(c => c.LastNumber)
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
