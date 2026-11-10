namespace Kuvora.Domain.Orders;

// Se guarda como texto en la base: agregar un canal nuevo (delivery) no exige migración.
public enum OrderChannel
{
    DineIn = 1,
    Counter = 2
}
