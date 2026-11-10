using Kuvora.Domain.Common;

namespace Kuvora.Domain.Orders;

// Raíz del agregado: los ítems solo se tocan a través del pedido, que protege las reglas.
// Las violaciones de datos lanzan ArgumentException; las de estado, InvalidOperationException.
// Los permisos (por ejemplo, que solo el Admin cancele lo enviado) los decide el servicio.
public class Order : Entity, ITenantEntity
{
    public const int GuestCountMin = 1;
    public const int GuestCountMax = 50;
    public const int ReasonMaxLength = 200;

    private readonly List<OrderItem> _items = [];

    public Guid TenantId { get; private set; }

    // Consecutivo por restaurante; lo entrega el contador atómico de la capa de aplicación.
    public long Number { get; private set; }

    public OrderChannel Channel { get; private set; }
    public Guid? TableId { get; private set; }
    public int? GuestCount { get; private set; }
    public Guid WaiterId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Open;
    public DateTimeOffset OpenedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancelReason { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    // El total no se guarda: se calcula con los ítems no cancelados.
    public long Total => _items.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => i.Subtotal);

    public bool HasPendingItems => _items.Any(i => i.Status == OrderItemStatus.Pending);
    public bool HasSentItems => _items.Any(i => i.Status == OrderItemStatus.Sent);

    // Última comanda enviada (0 si todavía no se envió ninguna).
    public int CurrentRound => _items.Max(i => i.Round) ?? 0;

    private Order() { }

    public Order(
        Guid tenantId, long number, OrderChannel channel, Guid? tableId, int? guestCount,
        Guid waiterId, DateTimeOffset openedAtUtc)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));

        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number), number, "El número de pedido debe ser positivo.");

        if (!Enum.IsDefined(channel))
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "El canal no es válido.");

        if (channel == OrderChannel.DineIn && (tableId is null || tableId == Guid.Empty))
            throw new ArgumentException("Un pedido de salón exige una mesa.", nameof(tableId));

        if (channel == OrderChannel.Counter && tableId is not null)
            throw new ArgumentException("Un pedido de mostrador no lleva mesa.", nameof(tableId));

        if (guestCount is < GuestCountMin or > GuestCountMax)
            throw new ArgumentOutOfRangeException(
                nameof(guestCount), guestCount, $"Las personas deben estar entre {GuestCountMin} y {GuestCountMax}.");

        if (waiterId == Guid.Empty)
            throw new ArgumentException("El mesero es obligatorio.", nameof(waiterId));

        TenantId = tenantId;
        Number = number;
        Channel = channel;
        TableId = tableId;
        GuestCount = guestCount;
        WaiterId = waiterId;
        OpenedAtUtc = openedAtUtc;
    }

    public OrderItem AddItem(
        Guid productId, string productName, long unitPrice, int quantity,
        string? note = null, int? dinerNumber = null)
    {
        EnsureOpen();
        EnsureDinerWithinGuestCount(dinerNumber);

        // El constructor del ítem valida el resto; si falla, nada se agrega.
        var item = new OrderItem(TenantId, Id, productId, productName, unitPrice, quantity, note, dinerNumber);
        _items.Add(item);
        return item;
    }

    public void UpdateItem(Guid itemId, int quantity, string? note, int? dinerNumber)
    {
        EnsureOpen();
        var item = GetItem(itemId);
        EnsureDinerWithinGuestCount(dinerNumber);

        item.Update(quantity, note, dinerNumber);
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureOpen();
        var item = GetItem(itemId);
        item.EnsurePending("quitar");

        _items.Remove(item);
    }

    // Pasa todos los ítems pendientes a "enviado" con la siguiente ronda y devuelve su número.
    // Sin pendientes lanza excepción: así un doble clic o un reintento no duplica la comanda.
    public int SendToKitchen(DateTimeOffset nowUtc)
    {
        EnsureOpen();

        var pending = _items.Where(i => i.Status == OrderItemStatus.Pending).ToList();
        if (pending.Count == 0)
            throw new InvalidOperationException("No hay ítems pendientes para enviar a cocina.");

        var round = CurrentRound + 1;
        foreach (var item in pending)
            item.MarkSent(round, nowUtc);

        return round;
    }

    // Solo para ítems ya enviados; los pendientes se quitan con RemoveItem.
    public void CancelItem(Guid itemId, Guid userId, string reason, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        var item = GetItem(itemId);

        if (item.Status != OrderItemStatus.Sent)
            throw new InvalidOperationException("Solo se puede cancelar un ítem enviado a cocina.");

        item.Cancel(userId, reason, nowUtc);
    }

    // Cancela el pedido y todos sus ítems vigentes con el mismo motivo y responsable.
    public void Cancel(Guid userId, string reason, DateTimeOffset nowUtc)
    {
        EnsureOpen();

        // Se valida antes de cambiar nada.
        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));
        var cleanReason = OrderItem.NormalizeReason(reason);

        foreach (var item in _items.Where(i => i.Status != OrderItemStatus.Cancelled))
            item.Cancel(userId, cleanReason, nowUtc);

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancelledByUserId = userId;
        CancelReason = cleanReason;
    }

    private void EnsureOpen()
    {
        if (Status != OrderStatus.Open)
            throw new InvalidOperationException("El pedido no está abierto.");
    }

    private void EnsureDinerWithinGuestCount(int? dinerNumber)
    {
        if (dinerNumber is { } diner && GuestCount is { } guests && diner > guests)
            throw new ArgumentOutOfRangeException(
                nameof(dinerNumber), diner, $"El comensal no puede superar las {guests} personas del pedido.");
    }

    private OrderItem GetItem(Guid itemId) =>
        _items.FirstOrDefault(i => i.Id == itemId)
        ?? throw new KeyNotFoundException("El ítem no existe en este pedido.");
}
