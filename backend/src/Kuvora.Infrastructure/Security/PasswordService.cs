using Kuvora.Application.Auth;
using Kuvora.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Kuvora.Infrastructure.Security;

internal sealed class PasswordService(IPasswordHasher<User> hasher) : IPasswordService
{
    // La implementación por defecto no usa el parámetro "user"; se pasa null!
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return hasher.HashPassword(null!, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        var result = hasher.VerifyHashedPassword(null!, passwordHash, password);
        return result != PasswordVerificationResult.Failed;
    }
}