using Kuvora.Domain.Catalog;
using Kuvora.Domain.Common;

namespace Kuvora.Domain.Orders;

// Pertenece al pedido: solo el Order lo crea y lo modifica (por eso los miembros son internal).
public class OrderItem : Entity, ITenantEntity
{
    public const int QuantityMin = 1;
    public const int QuantityMax = 99;
    public const int NoteMaxLength = 200;
    public const int DinerMin = 1;
    public const int DinerMax = 50;

    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }

    // Copia del nombre y del precio al momento de la venta: si el producto cambia después,
    // los pedidos antiguos no se alteran.
    public string ProductName { get; private set; } = null!;
    public long UnitPrice { get; private set; }

    public int Quantity { get; private set; }
    public string? Note { get; private set; }

    // Nulo = compartido entre todos los comensales.
    public int? DinerNumber { get; private set; }

    public OrderItemStatus Status { get; private set; } = OrderItemStatus.Pending;

    // Número de la comanda en que se envió (1, 2, 3…). Nulo mientras está pendiente.
    public int? Round { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancelReason { get; private set; }

    public long Subtotal => UnitPrice * Quantity;

    private OrderItem() { }

    internal OrderItem(
        Guid tenantId, Guid orderId, Guid productId, string productName, long unitPrice,
        int quantity, string? note, int? dinerNumber)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("El pedido es obligatorio.", nameof(orderId));
        if (productId == Guid.Empty)
            throw new ArgumentException("El producto es obligatorio.", nameof(productId));

        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        var trimmedName = productName.Trim();
        if (trimmedName.Length > Product.NameMaxLength)
            throw new ArgumentException(
                $"El nombre del producto no puede superar {Product.NameMaxLength} caracteres.", nameof(productName));

        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "El precio no puede ser negativo.");

        ValidateQuantity(quantity);
        var cleanNote = NormalizeNote(note);
        ValidateDiner(dinerNumber);

        TenantId = tenantId;
        OrderId = orderId;
        ProductId = productId;
        ProductName = trimmedName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Note = cleanNote;
        DinerNumber = dinerNumber;
    }

    // Valida todo antes de asignar: si algo falla, el ítem queda como estaba.
    internal void Update(int quantity, string? note, int? dinerNumber)
    {
        EnsurePending("editar");

        ValidateQuantity(quantity);
        var cleanNote = NormalizeNote(note);
        ValidateDiner(dinerNumber);

        Quantity = quantity;
        Note = cleanNote;
        DinerNumber = dinerNumber;
    }

    internal void MarkSent(int round, DateTimeOffset nowUtc)
    {
        EnsurePending("enviar");

        Status = OrderItemStatus.Sent;
        Round = round;
        SentAtUtc = nowUtc;
    }

    internal void Cancel(Guid userId, string reason, DateTimeOffset nowUtc)
    {
        if (Status == OrderItemStatus.Cancelled)
            throw new InvalidOperationException("El ítem ya está cancelado.");

        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));
        var cleanReason = NormalizeReason(reason);

        Status = OrderItemStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancelledByUserId = userId;
        CancelReason = cleanReason;
    }

    internal void EnsurePending(string action)
    {
        if (Status != OrderItemStatus.Pending)
            throw new InvalidOperationException($"Solo se puede {action} un ítem pendiente.");
    }

    internal static void ValidateDiner(int? dinerNumber)
    {
        if (dinerNumber is < DinerMin or > DinerMax)
            throw new ArgumentOutOfRangeException(
                nameof(dinerNumber), dinerNumber, $"El comensal debe estar entre {DinerMin} y {DinerMax}.");
    }

    internal static string NormalizeReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var trimmed = reason.Trim();
        if (trimmed.Length > Order.ReasonMaxLength)
            throw new ArgumentException(
                $"El motivo no puede superar {Order.ReasonMaxLength} caracteres.", nameof(reason));

        return trimmed;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < QuantityMin or > QuantityMax)
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity, $"La cantidad debe estar entre {QuantityMin} y {QuantityMax}.");
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > NoteMaxLength })
            throw new ArgumentException($"La nota no puede superar {NoteMaxLength} caracteres.", nameof(note));

        return trimmed;
    }
}
