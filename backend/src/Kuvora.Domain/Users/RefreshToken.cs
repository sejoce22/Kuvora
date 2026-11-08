using Kuvora.Domain.Common;

namespace Kuvora.Domain.Users;

public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && ExpiresAtUtc > now;

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        if (IsRevoked) return; // revocar dos veces no cambia nada

        RevokedAtUtc = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}