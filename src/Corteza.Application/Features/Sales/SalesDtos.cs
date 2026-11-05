using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Sales;

public sealed class OrderQuery : PageQuery
{
    public string? Code { get; init; }
    public OrderStatus? Status { get; init; }
    public ServiceType? ServiceType { get; init; }
    /// <summary>true = solo pedidos abiertos (sin cobrar ni cancelar).</summary>
    public bool? Open { get; init; }
}

public sealed record OrderListItem(
    int Id,
    string Code,
    ServiceType ServiceType,
    short? TableNumber,
    string? CustomerName,
    OrderStatus Status,
    decimal Total,
    int ItemsCount,
    string ItemsSummary,
    DateTime CreatedAt,
    bool BillRequested,
    bool IsClosed,
    string? InvoiceNumber);

/// <summary>Contadores de la pantalla 02.1.</summary>
public sealed record OrderSummary(int Open, int Preparing, int Ready, decimal SalesToday, int InvoicesToday);

public sealed record OrderItemDto(int ProductId, string ProductName, short Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderDetail(
    int Id,
    string Code,
    ServiceType ServiceType,
    int? DiningTableId,
    short? TableNumber,
    int? CustomerId,
    string? CustomerName,
    OrderStatus Status,
    decimal Total,
    string ServedBy,
    DateTime CreatedAt,
    DateTime? PreparingAt,
    DateTime? ReadyAt,
    DateTime? DeliveredAt,
    DateTime? BillRequestedAt,
    DateTime? ClosedAt,
    int? InvoiceId,
    string? InvoiceNumber,
    IReadOnlyList<OrderItemDto> Items);

public sealed class OrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el producto.")]
    public int ProductId { get; init; }

    /// <summary>Sin rango: el dominio responde "La cantidad debe ser mayor que cero".</summary>
    public short Quantity { get; init; }
}

public sealed class OrderCreateRequest
{
    [EnumDataType(typeof(ServiceType))]
    public ServiceType ServiceType { get; init; }
    public int? DiningTableId { get; init; }
    public int? CustomerId { get; init; }

    [MinLength(1, ErrorMessage = "Agrega al menos un producto al pedido.")]
    public List<OrderItemRequest> Items { get; init; } = [];
}

public sealed class AdvanceOrderRequest
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; init; }
}

public sealed class CheckoutRequest
{
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; init; }

    /// <summary>Solo en efectivo: lo que entrega el cliente.</summary>
    public decimal? AmountReceived { get; init; }
}

public sealed record DiningTableStatus(
    int Id,
    short Number,
    short Capacity,
    TableState State,
    int? OrderId,
    string? OrderCode,
    decimal? OrderTotal,
    DateTime? OpenedAt);

public sealed class SalesInvoiceQuery : PageQuery
{
    public string? Number { get; init; }
    public SalesInvoiceStatus? Status { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record SalesInvoiceListItem(
    int Id,
    string Number,
    int OrderId,
    string OrderCode,
    string? CustomerName,
    decimal Total,
    PaymentMethod PaymentMethod,
    SalesInvoiceStatus Status,
    DateTime IssuedAt);

public sealed record SalesInvoiceDetail(
    int Id,
    string Number,
    int OrderId,
    string OrderCode,
    ServiceType ServiceType,
    short? TableNumber,
    string? CustomerName,
    string? CustomerDocument,
    decimal Total,
    PaymentMethod PaymentMethod,
    decimal? AmountReceived,
    decimal? ChangeGiven,
    SalesInvoiceStatus Status,
    string IssuedBy,
    DateTime IssuedAt,
    string? VoidReason,
    DateTime? VoidedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed class VoidSalesInvoiceRequest
{
    [Required(ErrorMessage = "Indica el motivo de la anulación.")]
    [StringLength(200)]
    public string Reason { get; init; } = string.Empty;
}
