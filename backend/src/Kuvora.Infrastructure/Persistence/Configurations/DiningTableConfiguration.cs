using Kuvora.Domain.Tables;
using Kuvora.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kuvora.Infrastructure.Persistence.Configurations;

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.ToTable("dining_tables");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(DiningTable.NameMaxLength)
            .IsRequired();
        builder.Property(t => t.Zone)
            .HasMaxLength(DiningTable.ZoneMaxLength);
        builder.Property(t => t.Capacity)
            .IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}