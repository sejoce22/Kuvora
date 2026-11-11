using Kuvora.Domain.Users;

namespace Kuvora.Application.Common;

/// <summary>
/// El usuario que hace la petición actual, leído de su token.
/// Application solo conoce esta interfaz; Api la implementa con los claims.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }

    // Para reglas que dependen del rol y no se pueden resolver solo con la política del controlador
    // (por ejemplo, "el mesero cancela un pedido sin enviar, pero uno enviado solo lo cancela el Admin").
    UserRole Role { get; }
}
