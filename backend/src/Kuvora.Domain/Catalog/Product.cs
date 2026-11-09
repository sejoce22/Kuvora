using Kuvora.Domain.Common;

namespace Kuvora.Domain.Catalog;

public class Product : Entity, ITenantEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    public Guid TenantId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    // Pesos chilenos enteros: el peso no tiene decimales, por eso long y no decimal.
    public long Price { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Product() { }

    public Product(Guid tenantId, Guid categoryId, string name, long price, string? description = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));

        TenantId = tenantId;
        Update(categoryId, name, price, description);
    }

    // Valida todo antes de asignar: si algo falla, el producto queda como estaba.
    public void Update(Guid categoryId, string name, long price, string? description = null)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("La categoría es obligatoria.", nameof(categoryId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLength} caracteres.", nameof(name));

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > DescriptionMaxLength })
            throw new ArgumentException(
                $"La descripción no puede superar {DescriptionMaxLength} caracteres.", nameof(description));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "El precio no puede ser negativo.");

        CategoryId = categoryId;
        Name = trimmedName;
        Description = trimmedDescription;
        Price = price;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}