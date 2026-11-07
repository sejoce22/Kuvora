using Kuvora.Domain.Users;

namespace Kuvora.Domain.Tests.Users;

public class UserTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Constructor_WithValidData_NormalizesEmailAndFullName()
    {
        var user = new User(TenantId, "  Admin@Kuvora.COM ", "hash-falso", "  Ana Pérez ", UserRole.Admin);

        Assert.Equal("admin@kuvora.com", user.Email);
        Assert.Equal("Ana Pérez", user.FullName);
        Assert.Equal(TenantId, user.TenantId);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new User(Guid.Empty, "a@b.com", "hash-falso", "Ana", UserRole.Admin));
    }

    [Theory]
    [InlineData("correo-invalido")]
    [InlineData("   ")]
    [InlineData("")]
    public void Constructor_WithInvalidEmail_Throws(string email)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new User(TenantId, email, "hash-falso", "Ana", UserRole.Admin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankPasswordHash_Throws(string passwordHash)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new User(TenantId, "a@b.com", passwordHash, "Ana", UserRole.Admin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankFullName_Throws(string fullName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new User(TenantId, "a@b.com", "hash-falso", fullName, UserRole.Admin));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Constructor_WithUndefinedRole_Throws(int roleValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User(TenantId, "a@b.com", "hash-falso", "Ana", (UserRole)roleValue));
    }
}