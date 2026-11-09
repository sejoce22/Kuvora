using Kuvora.Api.Authorization;
using Kuvora.Application.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

// Leer: cualquier usuario autenticado (política por defecto). Escribir: solo Admin.
[ApiController]
[Route("api/tables")]
public class DiningTablesController(
    IDiningTableService tableService,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DiningTableDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DiningTableDto>>> GetAll(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Solo el Admin ve las inactivas; para el resto el parámetro se ignora
        var canSeeInactive = includeInactive && await authorization.IsAdminAsync(User);
        return Ok(await tableService.GetAllAsync(canSeeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<DiningTableDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiningTableDto>> Create(
        SaveDiningTableRequest request,
        CancellationToken cancellationToken)
    {
        var table = await tableService.CreateAsync(request, cancellationToken);
        return Created($"/api/tables/{table.Id}", table);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<DiningTableDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiningTableDto>> Update(
        Guid id,
        SaveDiningTableRequest request,
        CancellationToken cancellationToken) =>
        Ok(await tableService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<DiningTableDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiningTableDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await tableService.SetActiveAsync(id, true, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<DiningTableDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiningTableDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await tableService.SetActiveAsync(id, false, cancellationToken));
}
