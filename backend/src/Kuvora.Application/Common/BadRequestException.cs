namespace Kuvora.Application.Common;

// Para reglas de validación que dependen de datos de la base y por eso no caben en DataAnnotations
// (por ejemplo, "el comensal 5 no existe en un pedido de 4 personas"). Responde 400.
public sealed class BadRequestException(string message) : Exception(message);
