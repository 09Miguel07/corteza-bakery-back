using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;

namespace Corteza.Application.Features.Suppliers;

public sealed class SupplierQuery : PageQuery
{
    public string? TaxId { get; init; }
    public string? Name { get; init; }
    public int? CategoryId { get; init; }
    public bool? Active { get; init; }
}

public sealed record CategoryRef(int Id, string Name);

public sealed record SupplierListItem(
    int Id,
    string Name,
    string TaxId,
    string ContactName,
    string Phone,
    string Email,
    decimal AvailableBudget,
    bool IsActive,
    IReadOnlyList<CategoryRef> Categories);

public sealed record SupplierDetail(
    int Id,
    string Name,
    string TaxId,
    string ContactName,
    string Phone,
    string Email,
    decimal AvailableBudget,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CategoryRef> Categories,
    int ActivePurchaseOrders);

public sealed class SupplierUpsertRequest
{
    [Required(ErrorMessage = "El nombre del proveedor es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "El NIT es obligatorio.")]
    [RegularExpression(@"^[\d.\s]{9,14}(-\d)?$", ErrorMessage = "Escribe un NIT válido de 9 o 10 dígitos, por ejemplo 900.123.456-7.")]
    public string TaxId { get; init; } = string.Empty;

    [Required(ErrorMessage = "La persona de contacto es obligatoria.")]
    [StringLength(80)]
    public string ContactName { get; init; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^\+?[\d\s]{7,20}$", ErrorMessage = "Escribe un teléfono válido, por ejemplo 310 987 6543.")]
    public string Phone { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio: ahí se envían las órdenes de compra.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido, por ejemplo ventas@empresa.com.")]
    [StringLength(120)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Presupuesto disponible para aprobar órdenes de compra.</summary>
    [Range(0, 100_000_000_000, ErrorMessage = "El presupuesto no puede ser negativo.")]
    public decimal AvailableBudget { get; init; }

    [MinLength(1, ErrorMessage = "Marca al menos una categoría de lo que nos vende.")]
    public List<int> CategoryIds { get; init; } = [];
}
