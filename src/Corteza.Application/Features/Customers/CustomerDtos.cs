using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;

namespace Corteza.Application.Features.Customers;

public sealed class CustomerQuery : PageQuery
{
    /// <summary>Busca por cédula (parcial, ignora puntos).</summary>
    public string? Document { get; init; }

    /// <summary>Busca por nombre o apellido (parcial).</summary>
    public string? Name { get; init; }

    /// <summary>true = activos, false = inactivos, vacío = todos.</summary>
    public bool? Active { get; init; }
}

public sealed record CustomerListItem(
    int Id,
    string Code,
    string DocumentNumber,
    string FirstNames,
    string LastNames,
    string Phone,
    string? Email,
    bool IsActive);

public sealed record CustomerDetail(
    int Id,
    string Code,
    string DocumentNumber,
    string FirstNames,
    string LastNames,
    string Phone,
    string? Email,
    DateOnly? Birthday,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int OrdersCount,
    decimal TotalPurchased);

/// <summary>Campos editables de un cliente (la cédula no se edita, pantalla 01.3).</summary>
public class CustomerUpdateRequest
{
    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(80, ErrorMessage = "Los nombres no pueden pasar de 80 caracteres.")]
    public string FirstNames { get; init; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(80, ErrorMessage = "Los apellidos no pueden pasar de 80 caracteres.")]
    public string LastNames { get; init; } = string.Empty;

    [Required(ErrorMessage = "El teléfono celular es obligatorio.")]
    [RegularExpression(@"^\+?[\d\s]{7,20}$", ErrorMessage = "Escribe un teléfono válido, por ejemplo 312 458 9012.")]
    public string Phone { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Escribe un correo válido, por ejemplo maria.lopez@gmail.com.")]
    [StringLength(120)]
    public string? Email { get; init; }

    public DateOnly? Birthday { get; init; }
}

public sealed class CustomerCreateRequest : CustomerUpdateRequest
{
    [Required(ErrorMessage = "La cédula es obligatoria.")]
    [RegularExpression(@"^[\d.\s]{5,20}$", ErrorMessage = "La cédula solo puede tener números (los puntos son opcionales).")]
    public string DocumentNumber { get; init; } = string.Empty;
}
