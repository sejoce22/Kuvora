using System.ComponentModel.DataAnnotations;

namespace Kuvora.Application.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}