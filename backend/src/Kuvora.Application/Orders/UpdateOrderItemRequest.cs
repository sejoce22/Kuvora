using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Orders;

namespace Kuvora.Application.Orders;

// Reemplaza los tres campos editables del ítem. Sin valor por defecto en Quantity: si el cliente la omite
// llega 0, no pasa el rango y responde 400 (en vez de cambiar la cantidad sin que nadie lo pidiera).
public sealed class UpdateOrderItemRequest
{
    [Range(OrderItem.QuantityMin, OrderItem.QuantityMax)]
    public int Quantity { get; init; }

    [StringLength(OrderItem.NoteMaxLength)]
    public string? Note { get; init; }

    [Range(OrderItem.DinerMin, OrderItem.DinerMax)]
    public int? DinerNumber { get; init; }
}
