namespace Kuvora.Application.Common;

/// <summary>
/// Restaurante de la petición actual. Es null cuando la petición es anónima
/// (registro, login, refresh): en ese caso el filtro global no deja ver ningún dato.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}
