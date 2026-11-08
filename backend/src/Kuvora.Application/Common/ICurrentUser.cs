namespace Kuvora.Application.Common;

/// <summary>
/// El usuario que hace la petición actual, leído de su token.
/// Application solo conoce esta interfaz; Api la implementa con los claims.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }
}
