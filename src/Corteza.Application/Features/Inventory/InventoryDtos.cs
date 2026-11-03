using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Inventory;

/// <summary>Estado de existencias que muestra la lista de insumos (pantalla 03.1).</summary>
public enum StockStatus { Ok, Low, Out }

public sealed class SupplyQuery : PageQuery
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public StockStatus? Stock { get; init; }
    public bool? Active { get; init; } = true;
}

public sealed record SupplyListItem(
    int Id,
    string Name,
    int CategoryId,
    string CategoryName,
    string PurchaseUnit,
    string UnitLabel,
    decimal? UnitSizeKg,
    decimal CurrentStock,
    decimal MinStock,
    StockStatus Status,
    int? MainSupplierId,
    string? MainSupplierName,
    bool IsActive);

public class SupplyUpdateRequest
{
    [Required(ErrorMessage = "El nombre del insumo es obligatorio.")]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Elige la categoría del insumo.")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "La unidad de compra es obligatoria, por ejemplo \"Bulto de 50 kg\".")]
    [StringLength(60)]
    public string PurchaseUnit { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe cómo se cuenta, por ejemplo \"bultos\".")]
    [StringLength(20)]
    public string UnitLabel { get; init; } = string.Empty;

    [Range(0.001, 100000, ErrorMessage = "El peso por unidad debe ser mayor que cero.")]
    public decimal? UnitSizeKg { get; init; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad mínima no puede ser negativa.")]
    public decimal MinStock { get; init; }

    public int? MainSupplierId { get; init; }
}

public sealed class SupplyCreateRequest : SupplyUpdateRequest
{
    /// <summary>"Cantidad que hay ahora" (pantalla 03.2). Si es mayor que cero se registra como entrada inicial.</summary>
    [Range(0, 1_000_000, ErrorMessage = "La cantidad actual no puede ser negativa.")]
    public decimal InitialStock { get; init; }
}

public sealed class MovementQuery : PageQuery;

public sealed class MovementCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el insumo.")]
    public int SupplyId { get; init; }

    [EnumDataType(typeof(MovementType))]
    public MovementType Type { get; init; } = MovementType.In;

    [Range(0.01, 1_000_000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; init; }

    /// <summary>Quién lo trajo (solo entradas).</summary>
    public int? SupplierId { get; init; }

    /// <summary>Fecha de llegada o salida. Si no se envía, se usa hoy.</summary>
    public DateOnly? MovementDate { get; init; }

    [StringLength(60)]
    public string? Location { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    [StringLength(200)]
    public string? Notes { get; init; }
}

public sealed record MovementDto(
    long Id,
    int SupplyId,
    string SupplyName,
    MovementType Type,
    decimal Quantity,
    decimal StockAfter,
    int? SupplierId,
    string? SupplierName,
    DateOnly MovementDate,
    string? Location,
    DateOnly? ExpirationDate,
    string? Notes,
    string CreatedBy,
    DateTime CreatedAt);
