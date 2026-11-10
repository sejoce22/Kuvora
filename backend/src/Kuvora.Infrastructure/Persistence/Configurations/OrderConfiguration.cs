using Kuvora.Domain.Orders;
using Kuvora.Domain.Tables;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kuvora.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(o => o.CancelReason)
            .HasMaxLength(Order.ReasonMaxLength);

        // Concurrencia optimista: xmin es una columna del sistema de PostgreSQL que cambia en cada
        // UPDATE de la fila. Es una propiedad "sombra" (no existe en la clase) para no ensuciar el dominio.
        // EF la incluye en el WHERE de cada UPDATE/DELETE; si otra persona cambió la fila, no se actualiza
        // ninguna fila y EF lanza DbUpdateConcurrencyException.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Número consecutivo por restaurante; el contador evita repetidos y este índice es la red de seguridad.
        builder.HasIndex(o => new { o.TenantId, o.Number }).IsUnique();

        // Una mesa, un pedido abierto: índice único PARCIAL. Solo cuenta para las filas abiertas con mesa,
        // así una mesa puede tener muchos pedidos cancelados (y, en la Fase 6, cerrados) pero un solo abierto.
        builder.HasIndex(o => new { o.TenantId, o.TableId })
            .IsUnique()
            .HasFilter("status = 'Open' AND table_id IS NOT NULL")
            .HasDatabaseName("ux_orders_one_open_per_table");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DiningTable>()
            .WithMany()
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.WaiterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Los ítems viven y mueren con su pedido. EF llena el campo privado _items directamente.
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Propiedades calculadas, no son columnas
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.HasPendingItems);
        builder.Ignore(o => o.HasSentItems);
        builder.Ignore(o => o.CurrentRound);
    }
}
