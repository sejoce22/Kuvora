using Kuvora.Domain.Users;
using Kuvora.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace Kuvora.Infrastructure.Tests.Security;

public class PasswordServiceTests
{
    // Se usan las opciones por defecto para que los tests corran rápido
    private readonly PasswordService _sut = new(new PasswordHasher<User>());

    [Fact]
    public void Hash_DoesNotContainPlainPassword()
    {
        var hash = _sut.Hash("Clave-Segura-123");

        Assert.DoesNotContain("Clave-Segura-123", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var first = _sut.Hash("Clave-Segura-123");
        var second = _sut.Hash("Clave-Segura-123");

        Assert.NotEqual(first, second); // por la sal aleatoria
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("Clave-Segura-123");

        Assert.True(_sut.Verify(hash, "Clave-Segura-123"));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("Clave-Segura-123");

        Assert.False(_sut.Verify(hash, "otra-clave"));
    }
}