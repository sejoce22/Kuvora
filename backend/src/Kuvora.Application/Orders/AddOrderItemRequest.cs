using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Orders;

namespace Kuvora.Application.Orders;

public sealed class AddOrderItemRequest
{
    // Nullable + Required: un Guid normal llegaría como Guid.Empty si el cliente lo omite.
    [Required]
    public Guid? ProductId { get; init; }

    [Range(OrderItem.QuantityMin, OrderItem.QuantityMax)]
    public int Quantity { get; init; } = 1;

    [StringLength(OrderItem.NoteMaxLength)]
    public string? Note { get; init; }

    // Nulo = ítem compartido entre todos los comensales.
    [Range(OrderItem.DinerMin, OrderItem.DinerMax)]
    public int? DinerNumber { get; init; }
}
