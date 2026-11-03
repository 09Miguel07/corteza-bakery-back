namespace Corteza.Application.Common.Exceptions;

// Cada excepción se traduce a un código HTTP con ProblemDetails (ver GlobalExceptionHandler en la API).
// Los mensajes van en español y dicen qué corregir (RNF-18).

/// <summary>404: el recurso no existe.</summary>
public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity, object id) => new($"No se encontró {entity} con id {id}.");
}

/// <summary>409: choca con un dato que ya existe (cédula, NIT, nombre repetido).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>422: la petición es válida pero rompe una regla del negocio.</summary>
public sealed class BusinessRuleException(string message) : Exception(message);

/// <summary>401: usuario o contraseña incorrectos.</summary>
public sealed class InvalidCredentialsException() : Exception("Usuario o contraseña incorrectos.");
