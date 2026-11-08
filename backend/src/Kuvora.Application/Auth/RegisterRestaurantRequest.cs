using System.ComponentModel.DataAnnotations;

namespace Kuvora.Application.Auth;

public sealed class RegisterRestaurantRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string RestaurantName { get; init; } = null!;

    [Required, StringLength(120, MinimumLength = 1)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = null!;
}