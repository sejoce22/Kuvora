namespace Kuvora.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterRestaurantAsync(RegisterRestaurantRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}