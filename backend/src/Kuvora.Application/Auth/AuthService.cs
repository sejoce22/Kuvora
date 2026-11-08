using Kuvora.Application.Common;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Auth;

internal sealed class AuthService(
    IAppDbContext db,
    IPasswordService passwordService,
    ITokenService tokenService,
    IRefreshTokenGenerator refreshTokenGenerator,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResponse> RegisterRestaurantAsync(
        RegisterRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // El email es único en TODOS los restaurantes, por eso se salta el filtro global
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("Ya existe una cuenta con ese email.");

        var tenant = new Tenant(request.RestaurantName);
        var user = new User(
            tenant.Id,
            email,
            passwordService.Hash(request.Password),
            request.FullName,
            UserRole.Admin);

        db.Tenants.Add(tenant);
        db.Users.Add(user);

        // Un solo SaveChanges: restaurante, usuario y refresh token se guardan juntos o ninguno
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // La petición es anónima (aún no hay restaurante en el token): se salta el filtro global
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Mismo error para "no existe", "está inactivo" y "contraseña incorrecta"
        if (user is null
            || !user.IsActive
            || !passwordService.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = refreshTokenGenerator.Hash(request.RefreshToken);

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken)
            ?? throw new InvalidCredentialsException();

        // Un token ya usado que vuelve a aparecer = posible robo.
        // Se revocan todos los tokens activos de ese usuario y se obliga a iniciar sesión de nuevo.
        if (stored.IsRevoked)
        {
            await RevokeAllActiveAsync(stored.UserId, now, cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (!stored.IsActive(now))
            throw new InvalidCredentialsException();

        // Petición anónima: se salta el filtro global
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        // Rotación: el token usado queda revocado y apunta a su reemplazo
        var (plainToken, newToken) = CreateRefreshToken(user.Id);
        stored.Revoke(now, newToken.TokenHash);

        await db.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, plainToken, newToken.ExpiresAtUtc);
    }

    public async Task LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var hash = refreshTokenGenerator.Hash(request.RefreshToken);

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        // Idempotente: si no existe o ya estaba revocado, no hay nada que hacer
        if (stored is null || stored.IsRevoked)
            return;

        stored.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var (plainToken, refreshToken) = CreateRefreshToken(user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, plainToken, refreshToken.ExpiresAtUtc);
    }

    private (string PlainToken, RefreshToken Entity) CreateRefreshToken(Guid userId)
    {
        var plainToken = refreshTokenGenerator.Generate();

        var entity = new RefreshToken(
            userId,
            refreshTokenGenerator.Hash(plainToken),
            timeProvider.GetUtcNow() + refreshTokenGenerator.Lifetime);

        db.RefreshTokens.Add(entity);

        return (plainToken, entity);
    }

    private async Task RevokeAllActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke(now);

        await db.SaveChangesAsync(cancellationToken);
    }

    private AuthResponse CreateResponse(User user, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc)
    {
        var accessToken = tokenService.CreateAccessToken(user);

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString()));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
