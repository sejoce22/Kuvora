namespace Kuvora.Domain.Orders;

// En esta fase solo existen Open y Cancelled; "Closed" (pagado) llega con los cobros (Fase 6).
public enum OrderStatus
{
    Open = 1,
    Cancelled = 2
}
