using Kuvora.Domain.Users;

namespace Kuvora.Domain.Tests.Users;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken CreateToken(DateTimeOffset? expiresAt = null) =>
        new(Guid.CreateVersion7(), "hash-falso", expiresAt ?? Now.AddDays(7));

    [Fact]
    public void IsActive_BeforeExpiration_ReturnsTrue()
    {
        var token = CreateToken();

        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_AfterExpiration_ReturnsFalse()
    {
        var token = CreateToken(expiresAt: Now.AddDays(7));

        Assert.False(token.IsActive(Now.AddDays(8)));
    }

    [Fact]
    public void Revoke_MarksTokenAsInactiveAndStoresReplacement()
    {
        var token = CreateToken();

        token.Revoke(Now, "hash-nuevo");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(Now));
        Assert.Equal(Now, token.RevokedAtUtc);
        Assert.Equal("hash-nuevo", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Revoke_CalledTwice_KeepsTheFirstRevocation()
    {
        var token = CreateToken();

        token.Revoke(Now, "hash-uno");
        token.Revoke(Now.AddHours(1), "hash-dos");

        Assert.Equal(Now, token.RevokedAtUtc);
        Assert.Equal("hash-uno", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Constructor_WithEmptyUserId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RefreshToken(Guid.Empty, "hash-falso", Now.AddDays(7)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankTokenHash_Throws(string tokenHash)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RefreshToken(Guid.CreateVersion7(), tokenHash, Now.AddDays(7)));
    }
}
