using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kuvora.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // SHA-256 en Base64 ocupa 44 caracteres; 64 deja margen
        builder.Property(r => r.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(64);

        // Se busca siempre por hash: el índice único lo hace rápido y evita duplicados
        builder.HasIndex(r => r.TokenHash).IsUnique();

        // Si se borra el usuario, se borran sus tokens
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // IsRevoked es una propiedad calculada, no una columna
        builder.Ignore(r => r.IsRevoked);
    }
}
