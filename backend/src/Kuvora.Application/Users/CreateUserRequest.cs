using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Users;

namespace Kuvora.Application.Users;

public sealed class CreateUserRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = null!;

    // EnumDataType rechaza 0 y cualquier número que no sea un rol definido (devuelve 400, no 500)
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; }
}
