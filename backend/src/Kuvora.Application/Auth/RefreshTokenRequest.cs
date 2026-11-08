using System.ComponentModel.DataAnnotations;

namespace Kuvora.Application.Auth;

public sealed class RefreshTokenRequest
{
    // El máximo evita que alguien nos obligue a procesar textos enormes
    [Required, StringLength(200, MinimumLength = 1)]
    public string RefreshToken { get; init; } = null!;
}
