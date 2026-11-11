using Kuvora.Application.Common;
using Kuvora.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Orders;

internal sealed class OrderService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOrderNumberGenerator numberGenerator,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<OrderDto> OpenAsync(OpenOrderRequest request, CancellationToken cancellationToken)
    {
        // La validación del cuerpo (canal y mesa coherentes) ya la hizo [ApiController]:
        // aquí solo se revisa lo que depende de la base de datos.
        var channel = request.Channel!.Value;

        if (channel == OrderChannel.DineIn)
        {
            var tableId = request.TableId!.Value;

            var table = await db.DiningTables.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken)
                ?? throw new NotFoundException("La mesa no existe.");

            if (!table.IsActive)
                throw new ConflictException("La mesa está desactivada.");

            // Revisión amable para dar un 409 claro. La garantía real es el índice único parcial:
            // si dos meseros llegan a la vez, la base rechaza al segundo (23505, también 409).
            var occupied = await db.Orders.AnyAsync(
                o => o.TableId == tableId && o.Status == OrderStatus.Open,
                cancellationToken);

            if (occupied)
                throw new ConflictException("La mesa ya tiene un pedido abierto.");
        }

        // El número y el pedido se guardan en la MISMA transacción: si crear el pedido falla
        // (por ejemplo, la mesa se ocupó justo ahora), el número no se "gasta" y no quedan huecos.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var number = await numberGenerator.NextAsync(currentUser.TenantId, cancellationToken);

        var order = new Order(
            currentUser.TenantId,
            number,
            channel,
            request.TableId,
            request.GuestCount,
            currentUser.UserId,
            timeProvider.GetUtcNow());

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDtoAsync(order, cancellationToken);
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        // Un pedido de otro restaurante no pasa el filtro global: responde 404, igual que uno inexistente.
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("El pedido no existe.");

        return await ToDtoAsync(order, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOpenAsync(CancellationToken cancellationToken)
    {
        // Pocos pedidos abiertos a la vez (decenas): se cargan con sus ítems y se resume en memoria.
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Open)
            .OrderBy(o => o.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        var tableNames = await TableNamesAsync(orders.Select(o => o.TableId), cancellationToken);
        var waiterNames = await UserNamesAsync(orders.Select(o => o.WaiterId), cancellationToken);

        return orders
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.Number,
                o.Channel,
                o.TableId,
                o.TableId is { } tableId ? tableNames.GetValueOrDefault(tableId) : null,
                waiterNames.GetValueOrDefault(o.WaiterId, "—"),
                o.GuestCount,
                o.Items.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => i.Quantity),
                o.Total,
                o.OpenedAtUtc))
            .ToList();
    }

    public async Task<OrderDto> AddItemAsync(
        Guid orderId,
        AddOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOpenOrderForEditAsync(orderId, cancellationToken);
        EnsureDinerWithinGuestCount(order, request.DinerNumber);

        var productId = request.ProductId!.Value;

        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException("El producto no existe.");

        var categoryIsActive = await db.Categories.AnyAsync(
            c => c.Id == product.CategoryId && c.IsActive,
            cancellationToken);

        if (!product.IsActive || !categoryIsActive)
            throw new ConflictException("El producto no está disponible.");

        // El ítem guarda una copia del nombre y del precio de HOY: si el producto cambia mañana,
        // este pedido no se altera.
        order.AddItem(product.Id, product.Name, product.Price, request.Quantity, request.Note, request.DinerNumber);

        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(order, cancellationToken);
    }

    public async Task<OrderDto> UpdateItemAsync(
        Guid orderId,
        Guid itemId,
        UpdateOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOpenOrderForEditAsync(orderId, cancellationToken);
        var item = FindPendingItem(order, itemId, "editar");
        EnsureDinerWithinGuestCount(order, request.DinerNumber);

        order.UpdateItem(item.Id, request.Quantity, request.Note, request.DinerNumber);

        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(order, cancellationToken);
    }

    public async Task<OrderDto> RemoveItemAsync(Guid orderId, Guid itemId, CancellationToken cancellationToken)
    {
        var order = await LoadOpenOrderForEditAsync(orderId, cancellationToken);
        var item = FindPendingItem(order, itemId, "quitar");

        order.RemoveItem(item.Id);

        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(order, cancellationToken);
    }

    // Carga el pedido CON seguimiento (se va a modificar) y exige que siga abierto. Un pedido de otro
    // restaurante no pasa el filtro global y responde 404.
    private async Task<Order> LoadOpenOrderForEditAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("El pedido no existe.");

        if (order.Status != OrderStatus.Open)
            throw new ConflictException("El pedido no está abierto.");

        return order;
    }

    // Las reglas de estado se revisan aquí para devolver 404 o 409 con un mensaje claro.
    // El dominio las vuelve a exigir (red de seguridad), pero nunca debería llegar a lanzarlas.
    private static OrderItem FindPendingItem(Order order, Guid itemId, string action)
    {
        var item = order.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("El ítem no existe en este pedido.");

        if (item.Status != OrderItemStatus.Pending)
            throw new ConflictException(
                $"Solo se puede {action} un ítem pendiente; para uno enviado a cocina hay que cancelarlo.");

        return item;
    }

    private static void EnsureDinerWithinGuestCount(Order order, int? dinerNumber)
    {
        if (dinerNumber is { } diner && order.GuestCount is { } guests && diner > guests)
            throw new BadRequestException($"El comensal {diner} no existe: el pedido tiene {guests} personas.");
    }

    private async Task<OrderDto> ToDtoAsync(Order order, CancellationToken cancellationToken)
    {
        var tableNames = await TableNamesAsync([order.TableId], cancellationToken);
        var waiterNames = await UserNamesAsync([order.WaiterId], cancellationToken);

        return ToDto(
            order,
            order.TableId is { } tableId ? tableNames.GetValueOrDefault(tableId) : null,
            waiterNames);
    }

    private async Task<Dictionary<Guid, string>> TableNamesAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancellationToken)
    {
        var tableIds = ids.OfType<Guid>().Distinct().ToList();

        return await db.DiningTables.AsNoTracking()
            .Where(t => tableIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var userIds = ids.Distinct().ToList();

        return await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private static OrderDto ToDto(Order order, string? tableName, IReadOnlyDictionary<Guid, string> waiterNames) =>
        new(
            order.Id,
            order.Number,
            order.Channel,
            order.TableId,
            tableName,
            order.GuestCount,
            order.WaiterId,
            waiterNames.GetValueOrDefault(order.WaiterId, "—"),
            order.Status,
            order.OpenedAtUtc,
            order.Total,
            order.CurrentRound,
            order.Items
                .OrderBy(i => i.CreatedAtUtc)
                .ThenBy(i => i.Id)
                .Select(i => new OrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.ProductName,
                    i.UnitPrice,
                    i.Quantity,
                    i.Subtotal,
                    i.Note,
                    i.DinerNumber,
                    i.Status,
                    i.Round,
                    i.SentAtUtc))
                .ToList());
}
