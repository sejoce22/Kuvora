using System.Security.Claims;
using Kuvora.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterRestaurantAsync(request, cancellationToken);
        return Created("/api/auth/me", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RefreshAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<MeResponse> Me()
    {
        // Con MapInboundClaims = false, los claims conservan el nombre del token ("sub", "role"...)
        var response = new MeResponse(
            Guid.Parse(User.FindFirstValue("sub")!),
            Guid.Parse(User.FindFirstValue("tenant_id")!),
            User.FindFirstValue("email")!,
            User.FindFirstValue("role")!);

        return Ok(response);
    }
}

public sealed record MeResponse(Guid UserId, Guid TenantId, string Email, string Role);
