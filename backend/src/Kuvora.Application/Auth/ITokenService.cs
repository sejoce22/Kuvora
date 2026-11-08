using Kuvora.Domain.Users;

namespace Kuvora.Application.Auth;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}