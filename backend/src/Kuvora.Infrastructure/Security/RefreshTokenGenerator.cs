using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kuvora.Application.Auth;
using Microsoft.Extensions.Options;

namespace Kuvora.Infrastructure.Security;

internal sealed class RefreshTokenGenerator(IOptions<JwtOptions> options) : IRefreshTokenGenerator
{
    public TimeSpan Lifetime { get; } = TimeSpan.FromDays(options.Value.RefreshTokenDays);

    public string Generate()
    {
        // 64 bytes de un generador criptográficamente seguro, en Base64Url (seguro para URLs y JSON)
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // SHA-256 basta: el token ya es aleatorio y de alta entropía (no es una contraseña humana)
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes); // 44 caracteres
    }
}
