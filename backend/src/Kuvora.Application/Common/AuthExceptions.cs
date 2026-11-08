namespace Kuvora.Application.Common;

public sealed class ConflictException(string message) : Exception(message);

public sealed class InvalidCredentialsException() : Exception("Credenciales inválidas.");