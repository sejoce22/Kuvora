using Kuvora.Domain.Common;

namespace Kuvora.Domain.Orders;

// Una fila por restaurante con el último número de pedido entregado.
// No se modifica desde C#: la capa de aplicación la incrementa con una sola sentencia SQL atómica
// (INSERT ... ON CONFLICT DO UPDATE ... RETURNING), así dos pedidos simultáneos nunca reciben el mismo número.
public class OrderCounter : ITenantEntity
{
    public Guid TenantId { get; private set; }
    public long LastNumber { get; private set; }

    private OrderCounter() { }
}
