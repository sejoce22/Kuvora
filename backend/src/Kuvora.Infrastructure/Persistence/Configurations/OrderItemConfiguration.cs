using Kuvora.Domain.Catalog;
using Kuvora.Domain.Orders;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kuvora.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        // Copia del producto al momento de la venta (ver OrderItem)
        builder.Property(i => i.ProductName)
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();
        builder.Property(i => i.UnitPrice)
            .IsRequired();

        builder.Property(i => i.Note)
            .HasMaxLength(OrderItem.NoteMaxLength);
        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(i => i.CancelReason)
            .HasMaxLength(Order.ReasonMaxLength);

        // Mismo mecanismo de concurrencia que Order (ver OrderConfiguration): si dos personas
        // cambian el mismo ítem a la vez, la segunda recibe un conflicto en vez de pisar a la primera.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sin propiedad de navegación; Restrict: un producto que ya se vendió nunca se borra.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // La relación con el pedido (OrderId) se define en OrderConfiguration.

        builder.Ignore(i => i.Subtotal);
    }
}
