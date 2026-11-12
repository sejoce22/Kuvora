using Kuvora.Application.Catalog;
using Kuvora.Application.Common;
using Kuvora.Application.Orders;
using Kuvora.Application.Tables;
using Kuvora.Domain.Orders;
using Kuvora.Domain.Users;
using Kuvora.Infrastructure.Persistence;
using Kuvora.Infrastructure.Tests.Catalog;
using Kuvora.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Infrastructure.Tests.Orders;

/// <summary>Reglas de los pedidos contra PostgreSQL real: aislamiento, numeración, concurrencia y permisos.</summary>
public class OrderServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Un restaurante listo para usar: un admin, un mesero, una mesa y un producto.</summary>
    private sealed class Restaurant : IDisposable
    {
        public required Guid TenantId { get; init; }
        public required ServiceHost Admin { get; init; }
        public required ServiceHost Waiter { get; init; }
        public required Guid AdminId { get; init; }
        public required Guid WaiterId { get; init; }
        public required DiningTableDto Table { get; init; }
        public required CategoryDto Category { get; init; }
        public required ProductDto Product { get; init; }

        public void Dispose()
        {
            Admin.Dispose();
            Waiter.Dispose();
        }
    }

    private async Task<Restaurant> NewRestaurantAsync()
    {
        var tenantId = await ServiceHost.CreateTenantAsync(fixture, Ct);
        var adminId = await ServiceHost.CreateUserAsync(fixture, tenantId, UserRole.Admin, Ct);
        var waiterId = await ServiceHost.CreateUserAsync(fixture, tenantId, UserRole.Waiter, Ct);

        var admin = ServiceHost.For(fixture, tenantId, adminId, UserRole.Admin);
        var waiter = ServiceHost.For(fixture, tenantId, waiterId, UserRole.Waiter);

        var table = await admin.Tables.CreateAsync(new SaveDiningTableRequest { Name = "Mesa 1", Capacity = 4 }, Ct);
        var category = await admin.Categories.CreateAsync(new SaveCategoryRequest { Name = "Platos" }, Ct);
        var product = await admin.Products.CreateAsync(
            new SaveProductRequest { CategoryId = category.Id, Name = "Lomo saltado", Price = 5_000 }, Ct);

        return new Restaurant
        {
            TenantId = tenantId,
            Admin = admin,
            Waiter = waiter,
            AdminId = adminId,
            WaiterId = waiterId,
            Table = table,
            Category = category,
            Product = product
        };
    }

    private static OpenOrderRequest DineIn(Guid tableId, int? guests = 2) =>
        new() { Channel = OrderChannel.DineIn, TableId = tableId, GuestCount = guests };

    private static OpenOrderRequest Counter() => new() { Channel = OrderChannel.Counter };

    private static AddOrderItemRequest Item(Guid productId, int quantity = 1, string? note = null, int? diner = null) =>
        new() { ProductId = productId, Quantity = quantity, Note = note, DinerNumber = diner };

    private static CancelRequest Reason(string text = "Error del mesero") => new() { Reason = text };

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    // ---------- Apertura y numeración ----------

    [Fact]
    public async Task Open_DineIn_StartsAtNumberOne_WithWaiterAndTableNames()
    {
        using var r = await NewRestaurantAsync();

        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id, 3), Ct);

        Assert.Equal(1, order.Number);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(r.WaiterId, order.WaiterId);
        Assert.Equal("Mesa 1", order.TableName);
        Assert.Equal(3, order.GuestCount);
        Assert.Empty(order.Items);
    }

    [Fact]
    public async Task Open_Counter_Orders_GetConsecutiveNumbers_AndEachTenantStartsAtOne()
    {
        using var a = await NewRestaurantAsync();
        using var b = await NewRestaurantAsync();

        var a1 = await a.Admin.Orders.OpenAsync(Counter(), Ct);
        var a2 = await a.Admin.Orders.OpenAsync(Counter(), Ct);
        var a3 = await a.Admin.Orders.OpenAsync(Counter(), Ct);
        var b1 = await b.Admin.Orders.OpenAsync(Counter(), Ct);

        Assert.Equal([1L, 2L, 3L], new[] { a1.Number, a2.Number, a3.Number });
        Assert.Equal(1, b1.Number);
    }

    [Fact]
    public async Task Open_SecondOrderOnSameTable_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        await r.Admin.Orders.OpenAsync(DineIn(r.Table.Id), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct));
    }

    [Fact]
    public async Task Open_OnInactiveTable_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        await r.Admin.Tables.SetActiveAsync(r.Table.Id, false, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Admin.Orders.OpenAsync(DineIn(r.Table.Id), Ct));
    }

    [Fact]
    public async Task Open_OnTableOfAnotherTenant_ThrowsNotFound()
    {
        using var a = await NewRestaurantAsync();
        using var b = await NewRestaurantAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.OpenAsync(DineIn(a.Table.Id), Ct));
    }

    [Fact]
    public async Task Open_TwoAtOnceOnSameTable_OnlyOneWins_AndTheNumberIsNotWasted()
    {
        using var r = await NewRestaurantAsync();
        // Cada intento usa su propio host (su propio contexto), como dos peticiones reales llegando a la vez.
        // Ninguno puede ser r.Admin: un contexto que falló al guardar conserva el pedido rechazado en su
        // seguimiento y volvería a intentar insertarlo en la siguiente operación.
        using var first = ServiceHost.For(fixture, r.TenantId, r.AdminId, UserRole.Admin);
        using var second = ServiceHost.For(fixture, r.TenantId, r.WaiterId, UserRole.Waiter);

        var attempts = await Task.WhenAll(
            AttemptAsync(first, r.Table.Id),
            AttemptAsync(second, r.Table.Id));

        var winners = attempts.Where(a => a.Order is not null).ToList();
        var losers = attempts.Where(a => a.Error is not null).ToList();

        Assert.Single(winners);
        Assert.Single(losers);
        // El perdedor cae por la revisión amable (409) o por el índice único de la base (DbUpdateException).
        Assert.True(losers[0].Error is ConflictException or DbUpdateException, losers[0].Error!.ToString());

        // Como el número y el pedido van en la misma transacción, el intento fallido no gastó ningún número.
        var next = await r.Admin.Orders.OpenAsync(Counter(), Ct);
        Assert.Equal(winners[0].Order!.Number + 1, next.Number);

        static async Task<(OrderDto? Order, Exception? Error)> AttemptAsync(ServiceHost host, Guid tableId)
        {
            await Task.Yield();

            try
            {
                return (await host.Orders.OpenAsync(DineIn(tableId), CancellationToken.None), null);
            }
            catch (Exception ex)
            {
                return (null, ex);
            }
        }
    }

    [Fact]
    public async Task Open_ManyCounterOrdersAtOnce_GetUniqueConsecutiveNumbers()
    {
        using var r = await NewRestaurantAsync();
        const int total = 8;

        var hosts = Enumerable.Range(0, total)
            .Select(_ => ServiceHost.For(fixture, r.TenantId, r.WaiterId, UserRole.Waiter))
            .ToList();

        try
        {
            var orders = await Task.WhenAll(hosts.Select(async host =>
            {
                await Task.Yield();
                return await host.Orders.OpenAsync(Counter(), CancellationToken.None);
            }));

            var numbers = orders.Select(o => o.Number).Order().ToArray();

            Assert.Equal(Enumerable.Range(1, total).Select(n => (long)n).ToArray(), numbers);
        }
        finally
        {
            hosts.ForEach(h => h.Dispose());
        }
    }

    // ---------- Aislamiento entre restaurantes ----------

    [Fact]
    public async Task OtherTenant_CannotSeeOrTouchMyOrder()
    {
        using var a = await NewRestaurantAsync();
        using var b = await NewRestaurantAsync();
        var order = await a.Admin.Orders.OpenAsync(DineIn(a.Table.Id), Ct);

        var seenByB = await b.Admin.Orders.GetOpenAsync(Ct);

        Assert.DoesNotContain(seenByB, o => o.Id == order.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.GetAsync(order.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.AddItemAsync(order.Id, Item(b.Product.Id), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.SendToKitchenAsync(order.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.CancelOrderAsync(order.Id, Reason(), Ct));
    }

    [Fact]
    public async Task AddItem_ProductOfAnotherTenant_ThrowsNotFound()
    {
        using var a = await NewRestaurantAsync();
        using var b = await NewRestaurantAsync();
        var order = await b.Admin.Orders.OpenAsync(Counter(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => b.Admin.Orders.AddItemAsync(order.Id, Item(a.Product.Id), Ct));
    }

    // ---------- Ítems ----------

    [Fact]
    public async Task AddItem_SavesSnapshot_AndChangingTheProductDoesNotAlterTheOrder()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 2), Ct);

        await r.Admin.Products.UpdateAsync(
            r.Product.Id,
            new SaveProductRequest { CategoryId = r.Category.Id, Name = "Lomo premium", Price = 9_000 },
            Ct);

        var afterChange = await r.Waiter.Orders.GetAsync(order.Id, Ct);
        var item = Assert.Single(afterChange.Items);
        Assert.Equal("Lomo saltado", item.ProductName);
        Assert.Equal(5_000, item.UnitPrice);
        Assert.Equal(10_000, afterChange.Total);

        // Un ítem nuevo sí toma el precio actual.
        var withNew = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        Assert.Equal(19_000, withNew.Total);
        Assert.Equal(2, withNew.Items.Count);
    }

    [Fact]
    public async Task AddItem_InactiveProduct_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        await r.Admin.Products.SetActiveAsync(r.Product.Id, false, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct));
    }

    [Fact]
    public async Task AddItem_ProductOfInactiveCategory_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);

        // El servicio no deja desactivar una categoría con productos activos, así que se fuerza el caso
        // directo en la base: producto activo dentro de una categoría inactiva.
        await using (var db = new AppDbContext(fixture.Options, new FakeCurrentTenant(r.TenantId), TimeProvider.System))
        {
            var category = await db.Categories.SingleAsync(c => c.Id == r.Category.Id, Ct);
            category.Deactivate();
            await db.SaveChangesAsync(Ct);
        }

        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct));
    }

    [Fact]
    public async Task AddItem_DinerBeyondGuestCount_ThrowsBadRequest()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id, 2), Ct);

        await Assert.ThrowsAsync<BadRequestException>(() => r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, diner: 3), Ct));
    }

    [Fact]
    public async Task UpdateItem_ChangesQuantityNoteAndDiner_AndPersists()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id, 4), Ct);
        var withItem = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        var itemId = withItem.Items.Single().Id;

        await r.Waiter.Orders.UpdateItemAsync(
            order.Id, itemId, new UpdateOrderItemRequest { Quantity = 3, Note = "sin cebolla", DinerNumber = 2 }, Ct);

        var reloaded = await r.Admin.Orders.GetAsync(order.Id, Ct);
        var item = Assert.Single(reloaded.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal("sin cebolla", item.Note);
        Assert.Equal(2, item.DinerNumber);
        Assert.Equal(15_000, reloaded.Total);
    }

    [Fact]
    public async Task RemoveItem_DeletesTheRow()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        var withItems = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        withItems = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 2), Ct);
        var toRemove = withItems.Items.First(i => i.Quantity == 2).Id;

        await r.Waiter.Orders.RemoveItemAsync(order.Id, toRemove, Ct);

        var reloaded = await r.Admin.Orders.GetAsync(order.Id, Ct);
        var left = Assert.Single(reloaded.Items);
        Assert.Equal(1, left.Quantity);
        Assert.Equal(5_000, reloaded.Total);
    }

    [Fact]
    public async Task UpdateItem_ThatDoesNotExist_ThrowsNotFound()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            r.Waiter.Orders.UpdateItemAsync(order.Id, Guid.NewGuid(), new UpdateOrderItemRequest { Quantity = 1 }, Ct));
    }

    // ---------- Envío a cocina y rondas ----------

    [Fact]
    public async Task Send_MarksItemsAsSent_InRoundOne_AndASecondSendIsRejected()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 2), Ct);

        var sent = await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);

        Assert.All(sent.Items, i => Assert.Equal(OrderItemStatus.Sent, i.Status));
        Assert.Equal(1, sent.CurrentRound);
        Assert.Single(sent.Items.Select(i => i.Round).Distinct());

        // Doble clic o reintento de red: no debe crear otra ronda.
        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct));
        var reloaded = await r.Waiter.Orders.GetAsync(order.Id, Ct);
        Assert.Equal(1, reloaded.CurrentRound);
    }

    [Fact]
    public async Task Send_WithoutPendingItems_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct));
    }

    [Fact]
    public async Task Send_AfterAddingMoreItems_CreatesASecondRound()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);

        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 2), Ct);
        var second = await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);

        Assert.Equal(2, second.CurrentRound);
        Assert.Equal(2, second.Items.Select(i => i.Round).Distinct().Count());
        Assert.All(second.Items, i => Assert.Equal(OrderItemStatus.Sent, i.Status));
    }

    [Fact]
    public async Task UpdateAndRemove_SentItem_ThrowConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        var withItem = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        var itemId = withItem.Items.Single().Id;
        await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);

        await Assert.ThrowsAsync<ConflictException>(() =>
            r.Waiter.Orders.UpdateItemAsync(order.Id, itemId, new UpdateOrderItemRequest { Quantity = 5 }, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.RemoveItemAsync(order.Id, itemId, Ct));
    }

    // ---------- Cancelaciones y permisos ----------

    [Fact]
    public async Task CancelItem_Waiter_ThrowsForbidden_Admin_Succeeds_AndTotalDrops()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        var withItems = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        withItems = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 2), Ct);
        await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);
        var itemId = withItems.Items.First(i => i.Quantity == 2).Id;

        await Assert.ThrowsAsync<ForbiddenException>(() => r.Waiter.Orders.CancelItemAsync(order.Id, itemId, Reason(), Ct));

        var cancelled = await r.Admin.Orders.CancelItemAsync(order.Id, itemId, Reason("Se cayó el plato"), Ct);

        var item = cancelled.Items.Single(i => i.Id == itemId);
        Assert.Equal(OrderItemStatus.Cancelled, item.Status);
        Assert.Equal("Se cayó el plato", item.CancelReason);
        Assert.Equal(5_000, cancelled.Total);
    }

    [Fact]
    public async Task CancelItem_PendingItem_ThrowsConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        var withItem = await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);

        await Assert.ThrowsAsync<ConflictException>(() =>
            r.Admin.Orders.CancelItemAsync(order.Id, withItem.Items.Single().Id, Reason(), Ct));
    }

    [Fact]
    public async Task CancelOrder_WaiterWithoutSentItems_Succeeds_AndFreesTheTable()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);

        var cancelled = await r.Waiter.Orders.CancelOrderAsync(order.Id, Reason("Abierto por error"), Ct);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("Abierto por error", cancelled.CancelReason);

        // La mesa vuelve a estar libre y el número siguiente no se repite.
        var next = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);
        Assert.Equal(order.Number + 1, next.Number);
        Assert.DoesNotContain(await r.Waiter.Orders.GetOpenAsync(Ct), o => o.Id == order.Id);
    }

    [Fact]
    public async Task CancelOrder_WithSentItems_WaiterForbidden_AdminSucceeds()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct);
        await r.Waiter.Orders.SendToKitchenAsync(order.Id, Ct);

        await Assert.ThrowsAsync<ForbiddenException>(() => r.Waiter.Orders.CancelOrderAsync(order.Id, Reason(), Ct));

        var cancelled = await r.Admin.Orders.CancelOrderAsync(order.Id, Reason("Cliente se fue"), Ct);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.All(cancelled.Items, i => Assert.Equal(OrderItemStatus.Cancelled, i.Status));
        Assert.Equal(0, cancelled.Total);
    }

    [Fact]
    public async Task ChangesOnACancelledOrder_ThrowConflict()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);
        await r.Waiter.Orders.CancelOrderAsync(order.Id, Reason(), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id), Ct));
        await Assert.ThrowsAsync<ConflictException>(() => r.Waiter.Orders.CancelOrderAsync(order.Id, Reason(), Ct));
    }

    // ---------- Mesas y listado ----------

    [Fact]
    public async Task DeactivateTable_WithOpenOrder_ThrowsConflict_AndWorksAfterCancelling()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => r.Admin.Tables.SetActiveAsync(r.Table.Id, false, Ct));

        await r.Waiter.Orders.CancelOrderAsync(order.Id, Reason(), Ct);
        var table = await r.Admin.Tables.SetActiveAsync(r.Table.Id, false, Ct);

        Assert.False(table.IsActive);
    }

    [Fact]
    public async Task GetOpen_ListsOpenOrdersWithTableTotalAndItemCount()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(DineIn(r.Table.Id), Ct);
        await r.Waiter.Orders.AddItemAsync(order.Id, Item(r.Product.Id, 3), Ct);

        var open = await r.Admin.Orders.GetOpenAsync(Ct);

        var summary = Assert.Single(open);
        Assert.Equal(order.Id, summary.Id);
        Assert.Equal("Mesa 1", summary.TableName);
        Assert.Equal(15_000, summary.Total);
    }

    // ---------- Concurrencia optimista (xmin) ----------

    [Fact]
    public async Task TwoStaleCopiesOfTheSameOrder_SecondSaveThrowsConcurrencyException()
    {
        using var r = await NewRestaurantAsync();
        var order = await r.Waiter.Orders.OpenAsync(Counter(), Ct);

        // Dos "pantallas" cargan el mismo pedido; la segunda guarda con una copia ya vieja.
        await using var dbA = new AppDbContext(fixture.Options, new FakeCurrentTenant(r.TenantId), TimeProvider.System);
        await using var dbB = new AppDbContext(fixture.Options, new FakeCurrentTenant(r.TenantId), TimeProvider.System);

        var copyA = await dbA.Orders.Include(o => o.Items).SingleAsync(o => o.Id == order.Id, Ct);
        var copyB = await dbB.Orders.Include(o => o.Items).SingleAsync(o => o.Id == order.Id, Ct);

        copyA.AddItem(r.Product.Id, r.Product.Name, r.Product.Price, 1);
        await dbA.SaveChangesAsync(Ct);

        copyB.AddItem(r.Product.Id, r.Product.Name, r.Product.Price, 2);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));

        // Nada de la copia vieja quedó guardado.
        var reloaded = await r.Admin.Orders.GetAsync(order.Id, Ct);
        Assert.Single(reloaded.Items);
    }
}
