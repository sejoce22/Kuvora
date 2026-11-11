namespace Kuvora.Application.Common;

// El usuario está autenticado pero su rol no alcanza para esta acción. Responde 403.
public sealed class ForbiddenException(string message) : Exception(message);
