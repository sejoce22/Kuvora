using Kuvora.Domain.Common;

namespace Kuvora.Domain.Users;

public class User : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    private User() { }

    public User(Guid tenantId, string email, string passwordHash, string fullName, UserRole role)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "El rol no es válido.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@'))
            throw new ArgumentException("El email no es válido.", nameof(email));

        TenantId = tenantId;
        Email = normalizedEmail;
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
    }
}