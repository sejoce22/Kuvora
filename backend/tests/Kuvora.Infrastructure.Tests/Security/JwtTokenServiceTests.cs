using Kuvora.Domain.Users;
using Kuvora.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kuvora.Infrastructure.Tests.Security;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static JwtTokenService CreateSut() => new(
        Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = new string('k', 64),
            AccessTokenMinutes = 15
        }),
        new FixedTimeProvider(Now));

    private static User CreateUser() =>
        new(Guid.CreateVersion7(), "ana@kuvora.com", "hash-falso-123", "Ana", UserRole.Waiter);

    [Fact]
    public void CreateAccessToken_IncludesUserClaimsAndExpiry()
    {
        var user = CreateUser();

        var token = CreateSut().CreateAccessToken(user);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.TenantId.ToString(), jwt.GetClaim("tenant_id").Value);
        Assert.Equal("Waiter", jwt.GetClaim("role").Value);
        Assert.Equal(Now.AddMinutes(15), token.ExpiresAtUtc);
    }

    [Fact]
    public void CreateAccessToken_DoesNotIncludePasswordHash()
    {
        var user = CreateUser();

        var token = CreateSut().CreateAccessToken(user);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        Assert.DoesNotContain(jwt.Claims, claim => claim.Value == user.PasswordHash);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}