using System.ComponentModel.DataAnnotations;

namespace Kuvora.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = null!;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;
}