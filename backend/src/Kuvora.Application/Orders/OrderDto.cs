using Kuvora.Domain.Orders;

namespace Kuvora.Application.Orders;

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    long UnitPrice,
    int Quantity,
    long Subtotal,
    string? Note,
    int? DinerNumber,
    OrderItemStatus Status,
    int? Round,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? CancelReason);

// Detalle completo de un pedido (pantalla de Pedido).
public sealed record OrderDto(
    Guid Id,
    long Number,
    OrderChannel Channel,
    Guid? TableId,
    string? TableName,
    int? GuestCount,
    Guid WaiterId,
    string WaiterName,
    OrderStatus Status,
    DateTimeOffset OpenedAtUtc,
    long Total,
    int CurrentRound,
    DateTimeOffset? CancelledAtUtc,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items);

// Resumen para la sala: alimenta el estado de las mesas (monto y hora desde que se abrió).
public sealed record OrderSummaryDto(
    Guid Id,
    long Number,
    OrderChannel Channel,
    Guid? TableId,
    string? TableName,
    string WaiterName,
    int? GuestCount,
    int ItemCount,
    long Total,
    DateTimeOffset OpenedAtUtc);
