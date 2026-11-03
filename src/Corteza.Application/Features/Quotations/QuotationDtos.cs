using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Quotations;

public sealed class RfqQuery : PageQuery
{
    public string? Code { get; init; }
    public RfqStatus? Status { get; init; }
}

public sealed record RfqListItem(
    int Id,
    string Code,
    string? Description,
    RfqStatus Status,
    DateOnly ResponseDeadline,
    int ItemsCount,
    int QuotationsCount,
    decimal? LowestTotal,
    DateTime CreatedAt);

public sealed record RfqItemDto(int SupplyId, string SupplyName, string UnitLabel, decimal Quantity);

public sealed record QuotationItemDto(int SupplyId, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record QuotationDto(
    int Id,
    int SupplierId,
    string SupplierName,
    QuotationStatus Status,
    int DeliveryDays,
    decimal Total,
    string? Notes,
    DateTime ReceivedAt,
    IReadOnlyList<QuotationItemDto> Items);

public sealed record RfqDetail(
    int Id,
    string Code,
    string? Description,
    RfqStatus Status,
    DateOnly ResponseDeadline,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string CreatedBy,
    IReadOnlyList<RfqItemDto> Items,
    IReadOnlyList<QuotationDto> Quotations,
    int? LowestQuotationId,
    int? PurchaseOrderId,
    string? PurchaseOrderCode);

public sealed class RfqLineRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el insumo.")]
    public int SupplyId { get; init; }

    [Range(0.01, 1_000_000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; init; }
}

public sealed class RfqCreateRequest
{
    [StringLength(200)]
    public string? Description { get; init; }

    /// <summary>Hasta cuándo reciben cotizaciones los proveedores.</summary>
    public DateOnly ResponseDeadline { get; init; }

    [MinLength(1, ErrorMessage = "Agrega al menos un insumo a la solicitud.")]
    public List<RfqLineRequest> Items { get; init; } = [];
}

public sealed class QuotationPriceRequest
{
    [Range(1, int.MaxValue)]
    public int SupplyId { get; init; }

    /// <summary>Sin rango a propósito: el dominio responde "Monto inválido" (taller 2).</summary>
    public decimal UnitPrice { get; init; }
}

public sealed class QuotationCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el proveedor.")]
    public int SupplierId { get; init; }

    [Range(0, 365, ErrorMessage = "Los días de entrega deben estar entre 0 y 365.")]
    public int DeliveryDays { get; init; }

    [StringLength(200)]
    public string? Notes { get; init; }

    [MinLength(1, ErrorMessage = "Escribe el precio de cada insumo.")]
    public List<QuotationPriceRequest> Prices { get; init; } = [];
}

public sealed class SelectQuotationRequest
{
    /// <summary>Fecha de entrega de la orden. Si no se envía: hoy + días de entrega ofrecidos.</summary>
    public DateOnly? ExpectedDeliveryDate { get; init; }
}

public sealed record SelectQuotationResult(int PurchaseOrderId, string PurchaseOrderCode);
