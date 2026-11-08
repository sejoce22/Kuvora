using Kuvora.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Kuvora.Infrastructure.Tests.Security;

public class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _sut = new(Options.Create(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = new string('k', 64),
        RefreshTokenDays = 7
    }));

    [Fact]
    public void Generate_ReturnsDifferentTokenEachTime()
    {
        Assert.NotEqual(_sut.Generate(), _sut.Generate());
    }

    [Fact]
    public void Generate_ReturnsUrlSafeText()
    {
        var token = _sut.Generate();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Hash_SameToken_ProducesSameHash()
    {
        Assert.Equal(_sut.Hash("token-de-prueba"), _sut.Hash("token-de-prueba"));
    }

    [Fact]
    public void Hash_DiffersFromTokenAndFitsInDatabaseColumn()
    {
        var token = _sut.Generate();

        var hash = _sut.Hash(token);

        Assert.NotEqual(token, hash);
        Assert.Equal(44, hash.Length); // la columna token_hash admite hasta 64
    }

    [Fact]
    public void Lifetime_UsesConfiguredDays()
    {
        Assert.Equal(TimeSpan.FromDays(7), _sut.Lifetime);
    }
}
