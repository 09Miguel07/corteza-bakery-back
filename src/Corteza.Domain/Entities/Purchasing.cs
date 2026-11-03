using Corteza.Domain.Enums;

namespace Corteza.Domain.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string TaxId { get; set; } = null!; // NIT
    public string ContactName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SupplierSupplyCategory> CategoryLinks { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}

/// <summary>Qué categorías de insumo vende cada proveedor ("¿Qué nos vende?", pantalla 04.2).</summary>
public class SupplierSupplyCategory
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int SupplyCategoryId { get; set; }
    public SupplyCategory SupplyCategory { get; set; } = null!;
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // OC-1042, lo genera una secuencia
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Requested;
    public DateTime ExpectedDeliveryAt { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; private set; } // columna calculada
}

public class PurchaseInvoice
{
    public int Id { get; set; }
    public string SupplierInvoiceNumber { get; set; } = null!; // FC-9921, lo da el proveedor
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public string? Description { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Total { get; set; }
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Pending;
    public DateTime? PaidAt { get; set; }

    // "VENCIDA" no se guarda: es Pending con DueDate anterior a hoy.
}
