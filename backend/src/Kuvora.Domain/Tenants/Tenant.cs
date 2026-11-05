using Kuvora.Domain.Common;

namespace Kuvora.Domain.Tenants;

public class Tenant : Entity
{
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    // Requerido por EF Core para materializar la entidad desde la base de datos.
    private Tenant() { }

    public Tenant(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}
