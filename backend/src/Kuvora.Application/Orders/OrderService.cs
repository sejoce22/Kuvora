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
        string? tableName = null;

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

            tableName = table.Name;
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

        var waiterNames = await UserNamesAsync([order.WaiterId], cancellationToken);
        return ToDto(order, tableName, waiterNames);
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        // Un pedido de otro restaurante no pasa el filtro global: responde 404, igual que uno inexistente.
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("El pedido no existe.");

        var tableNames = await TableNamesAsync([order.TableId], cancellationToken);
        var waiterNames = await UserNamesAsync([order.WaiterId], cancellationToken);

        return ToDto(order, order.TableId is { } tableId ? tableNames.GetValueOrDefault(tableId) : null, waiterNames);
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
