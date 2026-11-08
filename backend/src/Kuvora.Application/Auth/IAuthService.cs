namespace Kuvora.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterRestaurantAsync(RegisterRestaurantRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}
