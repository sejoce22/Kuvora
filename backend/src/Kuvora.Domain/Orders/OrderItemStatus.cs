namespace Kuvora.Domain.Orders;

// Pending: todavía en manos del mesero. Sent: enviado a cocina. Cancelled: anulado.
// La Fase 5 agregará Preparing, Ready y Served; como se guarda como texto, no habrá migración.
public enum OrderItemStatus
{
    Pending = 1,
    Sent = 2,
    Cancelled = 3
}
