namespace Kuvora.Application.Orders;

/// <summary>
/// Entrega el siguiente número de pedido de un restaurante. Debe llamarse dentro de la misma transacción
/// que crea el pedido; la implementación (SQL atómico) vive en Infrastructure.
/// </summary>
public interface IOrderNumberGenerator
{
    Task<long> NextAsync(Guid tenantId, CancellationToken cancellationToken);
}
