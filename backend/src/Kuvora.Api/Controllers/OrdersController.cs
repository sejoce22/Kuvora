using Kuvora.Api.Authorization;
using Kuvora.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

// Admin, Mesero y Cajero. La cocina (Fase 5) y los cobros (Fase 6) tendrán sus propias políticas.
// Las reglas que dependen del estado del pedido (por ejemplo, qué puede cancelar un mesero) las aplica el servicio.
[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.ManageOrders)]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Open(OpenOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orderService.OpenAsync(request, cancellationToken);
        return Created($"/api/orders/{order.Id}", order);
    }

    // "open" no es un Guid, así que no choca con {id:guid}.
    [HttpGet("open")]
    [ProducesResponseType<IReadOnlyList<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetOpen(CancellationToken cancellationToken) =>
        Ok(await orderService.GetOpenAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> AddItem(
        Guid id,
        AddOrderItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.AddItemAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateItem(
        Guid id,
        Guid itemId,
        UpdateOrderItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.UpdateItemAsync(id, itemId, request, cancellationToken));

    // Devuelve el pedido actualizado (no 204): la pantalla necesita el total nuevo.
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> RemoveItem(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken) =>
        Ok(await orderService.RemoveItemAsync(id, itemId, cancellationToken));

    [HttpPost("{id:guid}/send")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Send(Guid id, CancellationToken cancellationToken) =>
        Ok(await orderService.SendToKitchenAsync(id, cancellationToken));

    // Doble candado: la política corta al mesero en la puerta (403) y el servicio vuelve a comprobar el rol.
    [HttpPost("{id:guid}/items/{itemId:guid}/cancel")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> CancelItem(
        Guid id,
        Guid itemId,
        CancelRequest request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.CancelItemAsync(id, itemId, request, cancellationToken));

    // Sin AdminOnly: el mesero puede cancelar un pedido sin ítems enviados; con enviados el servicio responde 403.
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Cancel(
        Guid id,
        CancelRequest request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.CancelOrderAsync(id, request, cancellationToken));
}
