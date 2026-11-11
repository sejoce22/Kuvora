using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Orders;

namespace Kuvora.Application.Orders;

// Motivo obligatorio al cancelar un ítem enviado o un pedido. Required rechaza también el texto en blanco.
public sealed class CancelRequest
{
    [Required, StringLength(Order.ReasonMaxLength)]
    public string Reason { get; init; } = null!;
}
