using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Purchasing;

/// <summary>Filtro de la pantalla 05.4: por pagar, vencidas o pagadas.</summary>
public enum PurchaseInvoiceView { Pending, Overdue, Paid }

public sealed class PurchaseInvoiceQuery : PageQuery
{
    public string? Number { get; init; }
    public int? SupplierId { get; init; }
    public PurchaseInvoiceView? View { get; init; }
}

public sealed record PurchaseInvoiceListItem(
    int Id,
    string SupplierInvoiceNumber,
    int SupplierId,
    string SupplierName,
    int? PurchaseOrderId,
    string? PurchaseOrderCode,
    string? Description,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Total,
    PurchaseInvoiceStatus Status,
    bool IsOverdue,
    DateTime? PaidAt);

public sealed record PurchaseInvoiceSummary(int PendingCount, decimal PendingTotal, int OverdueCount, decimal OverdueTotal, decimal DueThisWeekTotal);

public sealed class PurchaseInvoiceCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el proveedor.")]
    public int SupplierId { get; init; }

    [Required(ErrorMessage = "Escribe el número de la factura del proveedor.")]
    [StringLength(30)]
    public string SupplierInvoiceNumber { get; init; } = string.Empty;

    public int? PurchaseOrderId { get; init; }

    [StringLength(200)]
    public string? Description { get; init; }

    public DateOnly IssueDate { get; init; }
    public DateOnly DueDate { get; init; }

    /// <summary>Sin rango a propósito: el dominio responde "Monto inválido".</summary>
    public decimal Total { get; init; }
}
