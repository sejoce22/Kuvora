using Kuvora.Api.Authorization;
using Kuvora.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

// Leer: cualquier usuario autenticado (política por defecto). Escribir: solo Admin.
[ApiController]
[Route("api/categories")]
public class CategoriesController(
    ICategoryService categoryService,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Solo el Admin ve las inactivas; para el resto el parámetro se ignora
        var canSeeInactive = includeInactive && await authorization.IsAdminAsync(User);
        return Ok(await categoryService.GetAllAsync(canSeeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        SaveCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return Created($"/api/categories/{category.Id}", category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Update(
        Guid id,
        SaveCategoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await categoryService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await categoryService.SetActiveAsync(id, true, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await categoryService.SetActiveAsync(id, false, cancellationToken));
}
