using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Purchasing;

public sealed class PurchaseOrderQuery : PageQuery
{
    public string? Code { get; init; }
    public int? SupplierId { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
}

public sealed record PurchaseOrderListItem(
    int Id,
    string Code,
    int SupplierId,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateOnly ExpectedDeliveryDate,
    decimal Total,
    int ItemsCount,
    string ItemsSummary);

/// <summary>Contadores de la pantalla 05.1 ("Llegan hoy", "Por llegar esta semana").</summary>
public sealed record PurchaseOrderSummary(int Pending, int Approved, int ArrivingToday, int ArrivingThisWeek);

public sealed record PurchaseOrderItemDto(
    int SupplyId,
    string SupplyName,
    string UnitLabel,
    decimal? UnitSizeKg,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PurchaseOrderDetail(
    int Id,
    string Code,
    PurchaseOrderStatus Status,
    int SupplierId,
    string SupplierName,
    string SupplierTaxId,
    string SupplierPhone,
    decimal SupplierAvailableBudget,
    DateOnly ExpectedDeliveryDate,
    decimal Total,
    string? Notes,
    string? QuotationCode,
    DateTime RequestedAt,
    string CreatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? ReceivedAt,
    DateTime? CancelledAt,
    string? CancelReason,
    IReadOnlyList<PurchaseOrderItemDto> Items);

public sealed class PurchaseOrderLineRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el insumo.")]
    public int SupplyId { get; init; }

    [Range(0.01, 1_000_000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; init; }

    [Range(0.01, 1_000_000_000, ErrorMessage = "El precio unitario debe ser mayor que cero.")]
    public decimal UnitPrice { get; init; }
}

/// <summary>Pantalla 05.2: proveedor, insumos y fecha de entrega.</summary>
public sealed class PurchaseOrderCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el proveedor.")]
    public int SupplierId { get; init; }

    public DateOnly ExpectedDeliveryDate { get; init; }

    [StringLength(200)]
    public string? Notes { get; init; }

    [MinLength(1, ErrorMessage = "Agrega al menos un insumo a la orden.")]
    public List<PurchaseOrderLineRequest> Items { get; init; } = [];
}

public sealed class CancelPurchaseOrderRequest
{
    [Required(ErrorMessage = "Indica el motivo de la cancelación.")]
    [StringLength(200)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReceivePurchaseOrderRequest
{
    /// <summary>Fecha de llegada. Si no se envía, hoy.</summary>
    public DateOnly? ReceivedDate { get; init; }
}
