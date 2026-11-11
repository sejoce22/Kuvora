namespace Kuvora.Application.Orders;

public interface IOrderService
{
    Task<OrderDto> OpenAsync(OpenOrderRequest request, CancellationToken cancellationToken);
    Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderSummaryDto>> GetOpenAsync(CancellationToken cancellationToken);

    // Las operaciones sobre ítems devuelven el pedido completo: así la pantalla actualiza el total y las rondas.
    Task<OrderDto> AddItemAsync(Guid orderId, AddOrderItemRequest request, CancellationToken cancellationToken);
    Task<OrderDto> UpdateItemAsync(Guid orderId, Guid itemId, UpdateOrderItemRequest request, CancellationToken cancellationToken);
    Task<OrderDto> RemoveItemAsync(Guid orderId, Guid itemId, CancellationToken cancellationToken);

    Task<OrderDto> SendToKitchenAsync(Guid orderId, CancellationToken cancellationToken);
    Task<OrderDto> CancelItemAsync(Guid orderId, Guid itemId, CancelRequest request, CancellationToken cancellationToken);
    Task<OrderDto> CancelOrderAsync(Guid orderId, CancelRequest request, CancellationToken cancellationToken);
}
