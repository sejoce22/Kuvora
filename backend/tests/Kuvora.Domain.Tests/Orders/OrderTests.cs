using Kuvora.Domain.Orders;

namespace Kuvora.Domain.Tests.Orders;

public class OrderTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid WaiterId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 20, 0, 0, TimeSpan.Zero);

    private static Order NewDineInOrder(int? guests = 4) =>
        new(TenantId, 1, OrderChannel.DineIn, Guid.CreateVersion7(), guests, WaiterId, Now);

    private static OrderItem AddPastel(Order order, int quantity = 1, int? dinerNumber = null) =>
        order.AddItem(Guid.CreateVersion7(), "Pastel de choclo", 8900, quantity, dinerNumber: dinerNumber);

    // ---- Creación ----

    [Fact]
    public void Constructor_DineIn_StartsOpenAndEmpty()
    {
        var tableId = Guid.CreateVersion7();

        var order = new Order(TenantId, 7, OrderChannel.DineIn, tableId, 4, WaiterId, Now);

        Assert.Equal(TenantId, order.TenantId);
        Assert.Equal(7, order.Number);
        Assert.Equal(OrderChannel.DineIn, order.Channel);
        Assert.Equal(tableId, order.TableId);
        Assert.Equal(4, order.GuestCount);
        Assert.Equal(WaiterId, order.WaiterId);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(Now, order.OpenedAtUtc);
        Assert.Empty(order.Items);
        Assert.Equal(0, order.Total);
        Assert.Equal(0, order.CurrentRound);
    }

    [Fact]
    public void Constructor_Counter_WithoutTable_IsValid()
    {
        var order = new Order(TenantId, 1, OrderChannel.Counter, null, null, WaiterId, Now);

        Assert.Null(order.TableId);
        Assert.Null(order.GuestCount);
    }

    [Fact]
    public void Constructor_DineIn_WithoutTable_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(TenantId, 1, OrderChannel.DineIn, null, 2, WaiterId, Now));
        Assert.Throws<ArgumentException>(() =>
            new Order(TenantId, 1, OrderChannel.DineIn, Guid.Empty, 2, WaiterId, Now));
    }

    [Fact]
    public void Constructor_Counter_WithTable_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(TenantId, 1, OrderChannel.Counter, Guid.CreateVersion7(), null, WaiterId, Now));
    }

    [Fact]
    public void Constructor_WithEmptyTenantOrWaiter_Throws()
    {
        var tableId = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() =>
            new Order(Guid.Empty, 1, OrderChannel.DineIn, tableId, 2, WaiterId, Now));
        Assert.Throws<ArgumentException>(() =>
            new Order(TenantId, 1, OrderChannel.DineIn, tableId, 2, Guid.Empty, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_WithNonPositiveNumber_Throws(long number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(TenantId, number, OrderChannel.DineIn, Guid.CreateVersion7(), 2, WaiterId, Now));
    }

    [Fact]
    public void Constructor_WithUndefinedChannel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(TenantId, 1, (OrderChannel)99, null, null, WaiterId, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Constructor_WithGuestCountOutOfRange_Throws(int guests)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(TenantId, 1, OrderChannel.DineIn, Guid.CreateVersion7(), guests, WaiterId, Now));
    }

    // ---- Agregar ítems ----

    [Fact]
    public void AddItem_CopiesProductSnapshotAndStartsPending()
    {
        var order = NewDineInOrder();
        var productId = Guid.CreateVersion7();

        var item = order.AddItem(productId, "  Pisco sour ", 4500, 2, "  sin azúcar ", 3);

        Assert.Same(item, Assert.Single(order.Items));
        Assert.Equal(order.Id, item.OrderId);
        Assert.Equal(TenantId, item.TenantId);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Pisco sour", item.ProductName);
        Assert.Equal(4500, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("sin azúcar", item.Note);
        Assert.Equal(3, item.DinerNumber);
        Assert.Equal(OrderItemStatus.Pending, item.Status);
        Assert.Null(item.Round);
        Assert.Equal(9000, item.Subtotal);
    }

    [Fact]
    public void AddItem_WithBlankNote_StoresNull()
    {
        var order = NewDineInOrder();

        var item = order.AddItem(Guid.CreateVersion7(), "Pisco sour", 4500, 1, "   ");

        Assert.Null(item.Note);
    }

    [Fact]
    public void Total_SumsAllActiveItems()
    {
        var order = NewDineInOrder();

        AddPastel(order, quantity: 2);
        order.AddItem(Guid.CreateVersion7(), "Pisco sour", 4500, 1);

        Assert.Equal(2 * 8900 + 4500, order.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void AddItem_WithQuantityOutOfRange_ThrowsAndAddsNothing(int quantity)
    {
        var order = NewDineInOrder();

        Assert.Throws<ArgumentOutOfRangeException>(() => AddPastel(order, quantity));

        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_WithInvalidData_Throws()
    {
        var order = NewDineInOrder();
        var productId = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => order.AddItem(Guid.Empty, "Pisco sour", 4500, 1));
        Assert.ThrowsAny<ArgumentException>(() => order.AddItem(productId, "   ", 4500, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddItem(productId, "Pisco sour", -1, 1));
        Assert.Throws<ArgumentException>(() =>
            order.AddItem(productId, "Pisco sour", 4500, 1, new string('a', 201)));
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_WithDinerAboveGuestCount_Throws()
    {
        var order = NewDineInOrder(guests: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => AddPastel(order, dinerNumber: 3));

        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_WithDinerEqualToGuestCount_IsValid()
    {
        var order = NewDineInOrder(guests: 2);

        var item = AddPastel(order, dinerNumber: 2);

        Assert.Equal(2, item.DinerNumber);
    }

    [Fact]
    public void AddItem_WithoutGuestCount_AllowsAnyDinerInRange()
    {
        var order = NewDineInOrder(guests: null);

        Assert.Equal(50, AddPastel(order, dinerNumber: 50).DinerNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => AddPastel(order, dinerNumber: 51));
        Assert.Throws<ArgumentOutOfRangeException>(() => AddPastel(order, dinerNumber: 0));
    }

    // ---- Editar y quitar ----

    [Fact]
    public void UpdateItem_ChangesQuantityNoteAndDiner()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);

        order.UpdateItem(item.Id, 3, "  sin cilantro ", 2);

        Assert.Equal(3, item.Quantity);
        Assert.Equal("sin cilantro", item.Note);
        Assert.Equal(2, item.DinerNumber);
    }

    [Fact]
    public void UpdateItem_WithInvalidQuantity_KeepsPreviousValues()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order, quantity: 2, dinerNumber: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.UpdateItem(item.Id, 0, "otra nota", 2));

        Assert.Equal(2, item.Quantity);
        Assert.Null(item.Note);
        Assert.Equal(1, item.DinerNumber);
    }

    [Fact]
    public void UpdateItem_WithDinerAboveGuestCount_Throws()
    {
        var order = NewDineInOrder(guests: 2);
        var item = AddPastel(order);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.UpdateItem(item.Id, 1, null, 3));
    }

    [Fact]
    public void UpdateItem_OfSentItem_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);

        Assert.Throws<InvalidOperationException>(() => order.UpdateItem(item.Id, 2, null, null));
    }

    [Fact]
    public void UpdateItem_WithUnknownId_ThrowsKeyNotFound()
    {
        var order = NewDineInOrder();

        Assert.Throws<KeyNotFoundException>(() => order.UpdateItem(Guid.CreateVersion7(), 1, null, null));
    }

    [Fact]
    public void RemoveItem_OfPendingItem_RemovesIt()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);

        order.RemoveItem(item.Id);

        Assert.Empty(order.Items);
        Assert.Equal(0, order.Total);
    }

    [Fact]
    public void RemoveItem_OfSentItem_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);

        Assert.Throws<InvalidOperationException>(() => order.RemoveItem(item.Id));
        Assert.Single(order.Items);
    }

    // ---- Enviar a cocina ----

    [Fact]
    public void SendToKitchen_MarksPendingItemsAsSentInRoundOne()
    {
        var order = NewDineInOrder();
        var first = AddPastel(order);
        var second = AddPastel(order);
        var sentAt = Now.AddMinutes(5);

        var round = order.SendToKitchen(sentAt);

        Assert.Equal(1, round);
        Assert.Equal(1, order.CurrentRound);
        Assert.All(new[] { first, second }, item =>
        {
            Assert.Equal(OrderItemStatus.Sent, item.Status);
            Assert.Equal(1, item.Round);
            Assert.Equal(sentAt, item.SentAtUtc);
        });
        Assert.False(order.HasPendingItems);
        Assert.True(order.HasSentItems);
    }

    [Fact]
    public void SendToKitchen_WithoutPendingItems_Throws()
    {
        var order = NewDineInOrder();

        Assert.Throws<InvalidOperationException>(() => order.SendToKitchen(Now));
    }

    [Fact]
    public void SendToKitchen_Twice_DoesNotDuplicateTheRound()
    {
        var order = NewDineInOrder();
        AddPastel(order);
        order.SendToKitchen(Now);

        Assert.Throws<InvalidOperationException>(() => order.SendToKitchen(Now));
        Assert.Equal(1, order.CurrentRound);
    }

    [Fact]
    public void SendToKitchen_AfterAddingMore_CreatesNextRoundAndKeepsFirst()
    {
        var order = NewDineInOrder();
        var first = AddPastel(order);
        order.SendToKitchen(Now);
        var second = AddPastel(order);

        var round = order.SendToKitchen(Now.AddMinutes(10));

        Assert.Equal(2, round);
        Assert.Equal(1, first.Round);
        Assert.Equal(2, second.Round);
    }

    [Fact]
    public void SendToKitchen_OnCancelledOrder_Throws()
    {
        var order = NewDineInOrder();
        AddPastel(order);
        order.Cancel(AdminId, "Error de mesa", Now);

        Assert.Throws<InvalidOperationException>(() => order.SendToKitchen(Now));
    }

    // ---- Cancelar ítem ----

    [Fact]
    public void CancelItem_OfSentItem_RecordsWhoWhenAndWhy()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order, quantity: 2);
        var keep = AddPastel(order);
        order.SendToKitchen(Now);
        var cancelledAt = Now.AddMinutes(3);

        order.CancelItem(item.Id, AdminId, "  Cliente se arrepintió ", cancelledAt);

        Assert.Equal(OrderItemStatus.Cancelled, item.Status);
        Assert.Equal(cancelledAt, item.CancelledAtUtc);
        Assert.Equal(AdminId, item.CancelledByUserId);
        Assert.Equal("Cliente se arrepintió", item.CancelReason);
        Assert.Equal(keep.Subtotal, order.Total);
    }

    [Fact]
    public void CancelItem_OfPendingItem_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);

        Assert.Throws<InvalidOperationException>(() => order.CancelItem(item.Id, AdminId, "Motivo", Now));
    }

    [Fact]
    public void CancelItem_Twice_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);
        order.CancelItem(item.Id, AdminId, "Motivo", Now);

        Assert.Throws<InvalidOperationException>(() => order.CancelItem(item.Id, AdminId, "Otro", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CancelItem_WithBlankReason_ThrowsAndKeepsItemSent(string reason)
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);

        Assert.ThrowsAny<ArgumentException>(() => order.CancelItem(item.Id, AdminId, reason, Now));

        Assert.Equal(OrderItemStatus.Sent, item.Status);
    }

    [Fact]
    public void CancelItem_WithEmptyUser_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);

        Assert.Throws<ArgumentException>(() => order.CancelItem(item.Id, Guid.Empty, "Motivo", Now));
        Assert.Equal(OrderItemStatus.Sent, item.Status);
    }

    [Fact]
    public void CancelItem_WithTooLongReason_Throws()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.SendToKitchen(Now);

        Assert.Throws<ArgumentException>(() =>
            order.CancelItem(item.Id, AdminId, new string('a', Order.ReasonMaxLength + 1), Now));
    }

    // ---- Cancelar pedido ----

    [Fact]
    public void Cancel_MarksOrderAndAllActiveItemsCancelled()
    {
        var order = NewDineInOrder();
        var sent = AddPastel(order);
        order.SendToKitchen(Now);
        var pending = AddPastel(order);
        var cancelledAt = Now.AddMinutes(8);

        order.Cancel(AdminId, "  Mesa se retiró ", cancelledAt);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(cancelledAt, order.CancelledAtUtc);
        Assert.Equal(AdminId, order.CancelledByUserId);
        Assert.Equal("Mesa se retiró", order.CancelReason);
        Assert.All(new[] { sent, pending }, item =>
        {
            Assert.Equal(OrderItemStatus.Cancelled, item.Status);
            Assert.Equal(AdminId, item.CancelledByUserId);
            Assert.Equal("Mesa se retiró", item.CancelReason);
        });
        Assert.Equal(0, order.Total);
    }

    [Fact]
    public void Cancel_EmptyOrder_IsAllowed()
    {
        var order = NewDineInOrder();

        order.Cancel(WaiterId, "Abierta por error", Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WithBlankReason_ThrowsAndKeepsOrderOpen()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);

        Assert.ThrowsAny<ArgumentException>(() => order.Cancel(AdminId, "   ", Now));

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(OrderItemStatus.Pending, item.Status);
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var order = NewDineInOrder();
        order.Cancel(AdminId, "Motivo", Now);

        Assert.Throws<InvalidOperationException>(() => order.Cancel(AdminId, "Otro", Now));
    }

    [Fact]
    public void CancelledOrder_RejectsAnyChange()
    {
        var order = NewDineInOrder();
        var item = AddPastel(order);
        order.Cancel(AdminId, "Motivo", Now);

        Assert.Throws<InvalidOperationException>(() => AddPastel(order));
        Assert.Throws<InvalidOperationException>(() => order.UpdateItem(item.Id, 2, null, null));
        Assert.Throws<InvalidOperationException>(() => order.RemoveItem(item.Id));
    }
}
