using Corteza.Application.Common.Exceptions;
using Corteza.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Api.Errors;

/// <summary>Convierte las excepciones en ProblemDetails (RFC 7807) con mensajes en español.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "No encontrado", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Dato repetido", e.Message),
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "No se puede completar la acción", e.Message),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "No se puede completar la acción", e.Message),
            InvalidCredentialsException e => (StatusCodes.Status401Unauthorized, "No autorizado", e.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado", "Inicia sesión de nuevo."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Datos desactualizados",
                "Alguien más cambió este registro mientras lo editabas. Recarga la página e inténtalo de nuevo."),
            // Choque con un índice único que se coló entre la validación y el guardado.
            DbUpdateException => (StatusCodes.Status409Conflict, "No se pudo guardar",
                "Otro usuario modificó o registró el mismo dato. Recarga la página e inténtalo de nuevo."),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado",
                "Ocurrió un error inesperado. Inténtalo de nuevo y, si sigue pasando, avisa al equipo de soporte."),
        };

        if (status >= 500)
            logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
        else if (exception is DbUpdateException)
            logger.LogWarning(exception, "Conflicto al guardar en {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
