namespace Kuvora.Application.Orders;

public interface IOrderService
{
    Task<OrderDto> OpenAsync(OpenOrderRequest request, CancellationToken cancellationToken);
    Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderSummaryDto>> GetOpenAsync(CancellationToken cancellationToken);
}
