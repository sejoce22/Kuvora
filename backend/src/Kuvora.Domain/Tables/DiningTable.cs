using Kuvora.Domain.Common;

namespace Kuvora.Domain.Tables;

public class DiningTable : Entity, ITenantEntity
{
    public const int NameMaxLength = 40;
    public const int ZoneMaxLength = 40;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 50;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public int Capacity { get; private set; }

    // Texto libre opcional ("Terraza", "Salón"); si luego hace falta, se convierte en entidad.
    public string? Zone { get; private set; }

    public bool IsActive { get; private set; } = true;

    private DiningTable() { }

    public DiningTable(Guid tenantId, string name, int capacity, string? zone = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));

        TenantId = tenantId;
        Update(name, capacity, zone);
    }

    // Valida todo antes de asignar: si algo falla, la mesa queda como estaba.
    public void Update(string name, int capacity, string? zone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new ArgumentException($"El nombre no puede superar {NameMaxLength} caracteres.", nameof(name));

        if (capacity is < MinCapacity or > MaxCapacity)
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, $"La capacidad debe estar entre {MinCapacity} y {MaxCapacity}.");

        var trimmedZone = string.IsNullOrWhiteSpace(zone) ? null : zone.Trim();
        if (trimmedZone is { Length: > ZoneMaxLength })
            throw new ArgumentException($"La zona no puede superar {ZoneMaxLength} caracteres.", nameof(zone));

        Name = trimmedName;
        Capacity = capacity;
        Zone = trimmedZone;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}