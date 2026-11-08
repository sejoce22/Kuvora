namespace Kuvora.Application.Auth;

public interface IRefreshTokenGenerator
{
    /// <summary>Cuánto dura un refresh token desde que se emite.</summary>
    TimeSpan Lifetime { get; }

    /// <summary>Crea un token aleatorio. Es lo que se entrega al cliente (nunca se guarda en claro).</summary>
    string Generate();

    /// <summary>Calcula el hash que se guarda en la base de datos.</summary>
    string Hash(string token);
}
