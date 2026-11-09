using Kuvora.Domain.Common;

namespace Kuvora.Domain.Catalog;

public class Category : Entity, ITenantEntity
{
    public const int NameMaxLength = 60;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Category() { }

    public Category(Guid tenantId, string name, int sortOrder = 0)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));

        TenantId = tenantId;
        Update(name, sortOrder);
    }

    // Valida primero y asigna después: si algo falla, el estado anterior queda intacto.
    public void Update(string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLength} caracteres.", nameof(name));

        if (sortOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "El orden no puede ser negativo.");

        Name = trimmedName;
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}