using Kuvora.Application.Common;
using Kuvora.Domain.Tenants;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Auth;

internal sealed class AuthService(
    IAppDbContext db,
    IPasswordService passwordService,
    ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterRestaurantAsync(
        RegisterRestaurantRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // Paso 7: esta consulta llevará IgnoreQueryFilters() (el email es único global)
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
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
        await db.SaveChangesAsync(cancellationToken); // un solo SaveChanges: atómico

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // Paso 7: aquí también irá IgnoreQueryFilters()
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Mismo error para "no existe", "está inactivo" y "contraseña incorrecta"
        if (user is null
            || !user.IsActive
            || !passwordService.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokenService.CreateAccessToken(user);

        return new AuthResponse(
            token.Value,
            token.ExpiresAtUtc,
            new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString()));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}