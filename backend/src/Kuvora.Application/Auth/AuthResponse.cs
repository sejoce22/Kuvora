namespace Kuvora.Application.Auth;

public sealed record UserDto(Guid Id, Guid TenantId, string Email, string FullName, string Role);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    UserDto User);
