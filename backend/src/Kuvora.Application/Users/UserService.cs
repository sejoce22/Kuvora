using Kuvora.Application.Auth;
using Kuvora.Application.Common;
using Kuvora.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Users;

internal sealed class UserService(
    IAppDbContext db,
    IPasswordService passwordService,
    ICurrentUser currentUser) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        // Sin Where por TenantId: el filtro global de AppDbContext ya limita a mi restaurante
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.TenantId, u.Email, u.FullName, u.Role })
            .ToListAsync(cancellationToken);

        return users
            .Select(u => new UserDto(u.Id, u.TenantId, u.Email, u.FullName, u.Role.ToString()))
            .ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // El email es único en TODOS los restaurantes, por eso se salta el filtro
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("Ya existe una cuenta con ese email.");

        // El nuevo usuario siempre queda en el restaurante de quien lo crea
        var user = new User(
            currentUser.TenantId,
            email,
            passwordService.Hash(request.Password),
            request.FullName,
            request.Role);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString());
    }
}
