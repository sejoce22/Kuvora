using Kuvora.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kuvora.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual del recurso."),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado."),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Credenciales inválidas."),
            // Carrera: dos peticiones pasaron la validación a la vez y la base de datos frenó a la segunda
            DbUpdateException when IsUniqueViolation(exception) =>
                (StatusCodes.Status409Conflict, "Conflicto con el estado actual del recurso."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Excepción no controlada en {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            // Error esperado: sin stack trace, solo una línea
            logger.LogWarning(
                "{ExceptionType} en {Method} {Path}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Solo el mensaje de NUESTRAS excepciones de negocio, nunca el de una cualquiera
                Detail = exception switch
                {
                    ConflictException or NotFoundException => exception.Message,
                    // Texto fijo: el mensaje de Postgres trae nombres de tablas e índices
                    DbUpdateException when IsUniqueViolation(exception) => "Ya existe un registro con esos datos.",
                    _ => null
                }
            }
        });
    }

    // 23505 = unique_violation
    private static bool IsUniqueViolation(Exception exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}