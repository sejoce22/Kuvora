using System.Security.Claims;
using Kuvora.Application.Auth;
using Kuvora.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kuvora.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    // El refresh token viaja SOLO en una cookie HttpOnly: JavaScript no puede leerla (protege frente a XSS).
    private const string RefreshCookieName = "kuvora_refresh";

    // La cookie solo se envía a /api/auth/*, es decir, a refresh y logout; nunca al resto de la API.
    private const string RefreshCookiePath = "/api/auth";

    private const int MaxRefreshTokenLength = 200;

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionResponse>> Register(
        RegisterRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterRestaurantAsync(request, cancellationToken);
        SetRefreshCookie(response);
        return Created("/api/auth/me", ToSession(response));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        SetRefreshCookie(response);
        return Ok(ToSession(response));
    }

    // Sin cuerpo: el refresh token llega en la cookie.
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = ReadRefreshCookie() ?? throw new InvalidCredentialsException();

        var response = await authService.RefreshAsync(
            new RefreshTokenRequest { RefreshToken = token },
            cancellationToken);

        // Rotación: la cookie se reemplaza por el token nuevo
        SetRefreshCookie(response);
        return Ok(ToSession(response));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = ReadRefreshCookie();

        if (token is not null)
        {
            await authService.LogoutAsync(
                new RefreshTokenRequest { RefreshToken = token },
                cancellationToken);
        }

        // Idempotente: siempre se borra la cookie, tenga o no token válido
        DeleteRefreshCookie();
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

    private string? ReadRefreshCookie()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var token)
            || string.IsNullOrWhiteSpace(token)
            || token.Length > MaxRefreshTokenLength)
        {
            return null;
        }

        return token;
    }

    private void SetRefreshCookie(AuthResponse response)
    {
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, CreateCookieOptions(response.RefreshTokenExpiresAtUtc));
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(expires: null));
    }

    private static CookieOptions CreateCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,                    // JavaScript no puede leerla
        Secure = true,                      // solo por HTTPS (localhost cuenta como seguro en Chrome y Edge)
        SameSite = SameSiteMode.Strict,     // el navegador no la envía desde otros sitios (defensa frente a CSRF)
        Path = RefreshCookiePath,
        Expires = expires
    };

    // Lo que ve el cliente: el access token y el usuario. El refresh token NUNCA va en el cuerpo.
    private static SessionResponse ToSession(AuthResponse response) =>
        new(response.AccessToken, response.ExpiresAtUtc, response.User);
}

public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserDto User);

public sealed record MeResponse(Guid UserId, Guid TenantId, string Email, string Role);
