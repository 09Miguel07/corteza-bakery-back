using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

/// <summary>Línea de una orden: insumo, cantidad y precio unitario acordado.</summary>
public readonly record struct PurchaseOrderLine(int SupplyId, decimal Quantity, decimal UnitPrice);

/// <summary>
/// Orden de compra (OC-1042). Reglas del taller 3: no se aprueba si excede el presupuesto del proveedor,
/// una orden aprobada no se vuelve a aprobar, la aprobación descuenta el presupuesto y solo se cancela
/// una orden pendiente indicando el motivo. Al recibirla, cada línea entra al inventario.
/// </summary>
public class PurchaseOrder
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // OC-1042, lo genera una secuencia
    public int SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Pending;
    public DateOnly ExpectedDeliveryDate { get; private set; }
    public decimal Total { get; private set; }
    public string? Notes { get; private set; }
    public int? QuotationId { get; private set; }
    public Quotation? Quotation { get; set; }

    public int CreatedByUserId { get; private set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public int? ApprovedByUserId { get; private set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; private set; }

    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = [];

    /// <summary>Pendiente o aprobada: todavía no llega y bloquea la desactivación del proveedor.</summary>
    public bool IsActive => Status is PurchaseOrderStatus.Pending or PurchaseOrderStatus.Approved;

    private string Label => string.IsNullOrEmpty(Code) ? "La orden" : $"La orden {Code}";

    private PurchaseOrder() { } // EF Core

    public static PurchaseOrder Create(Supplier supplier, IEnumerable<PurchaseOrderLine> lines, DateOnly expectedDeliveryDate,
        DateOnly today, int createdByUserId, string? notes = null)
    {
        if (!supplier.IsActive)
            throw new InactiveSupplierException(supplier.Name);

        var items = lines.ToList();
        if (items.Count == 0)
            throw new InvalidPurchaseOrderException("Agrega al menos un insumo a la orden.");
        if (items.Any(l => l.Quantity <= 0))
            throw new InvalidQuantityException();
        if (items.Any(l => l.UnitPrice <= 0))
            throw new InvalidAmountException("Monto inválido: el precio unitario debe ser mayor que cero.");
        if (items.Select(l => l.SupplyId).Distinct().Count() != items.Count)
            throw new InvalidPurchaseOrderException("Un insumo no se puede repetir en la misma orden.");
        if (expectedDeliveryDate < today)
            throw new InvalidPurchaseOrderException("La fecha de entrega no puede ser anterior a hoy.");

        var order = new PurchaseOrder
        {
            SupplierId = supplier.Id,
            Supplier = supplier,
            ExpectedDeliveryDate = expectedDeliveryDate,
            CreatedByUserId = createdByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        foreach (var line in items)
            order.Items.Add(new PurchaseOrderItem { SupplyId = line.SupplyId, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

        order.Total = order.Items.Sum(i => i.LineTotal);
        return order;
    }

    /// <summary>Crea la orden a partir de la cotización ganadora, con sus precios.</summary>
    public static PurchaseOrder FromQuotation(Quotation quotation, DateOnly expectedDeliveryDate, DateOnly today, int createdByUserId)
    {
        if (quotation.Status != QuotationStatus.Selected)
            throw new InvalidPurchaseOrderException("Solo la cotización ganadora genera una orden de compra.");

        var order = Create(quotation.Supplier,
            quotation.Items.Select(i => new PurchaseOrderLine(i.SupplyId, i.Quantity, i.UnitPrice)),
            expectedDeliveryDate, today, createdByUserId,
            notes: string.IsNullOrEmpty(quotation.RequestForQuotation?.Code) ? null : $"Generada desde la solicitud {quotation.RequestForQuotation.Code}");
        order.QuotationId = quotation.Id;
        order.Quotation = quotation;
        return order;
    }

    /// <summary>Aprueba la orden y descuenta su total del presupuesto del proveedor.</summary>
    public void Approve(Supplier supplier, int approvedByUserId, DateTime now)
    {
        if (supplier.Id != SupplierId)
            throw new InvalidPurchaseOrderException("El presupuesto debe ser el del proveedor de la orden.");
        if (Status == PurchaseOrderStatus.Approved)
            throw new InvalidOrderStateException($"{Label} ya está aprobada.");
        if (Status != PurchaseOrderStatus.Pending)
            throw new InvalidOrderStateException($"{Label} no está pendiente: solo se aprueban órdenes pendientes.");

        supplier.ReserveBudget(Total); // si no alcanza, lanza y la orden sigue pendiente

        Status = PurchaseOrderStatus.Approved;
        ApprovedAt = now;
        ApprovedByUserId = approvedByUserId;
    }

    public void Cancel(string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidPurchaseOrderException("Indica el motivo de la cancelación.");
        if (Status != PurchaseOrderStatus.Pending)
            throw new InvalidOrderStateException($"{Label} no está pendiente: solo se cancelan órdenes pendientes.");

        Status = PurchaseOrderStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = now;
    }

    /// <summary>
    /// Recibe la mercancía: cada línea entra al inventario con su movimiento ligado a la orden y al proveedor.
    /// </summary>
    public IReadOnlyList<InventoryMovement> Receive(IReadOnlyDictionary<int, Supply> supplies, int receivedByUserId,
        DateOnly receivedDate, DateTime now)
    {
        if (Status != PurchaseOrderStatus.Approved)
            throw new InvalidOrderStateException($"{Label} no está aprobada: solo se reciben órdenes aprobadas.");
        if (Items.Any(i => !supplies.ContainsKey(i.SupplyId)))
            throw new InvalidPurchaseOrderException("Faltan insumos de la orden para registrar la recepción.");

        var movements = Items
            .Select(i => supplies[i.SupplyId].RegisterEntry(i.Quantity, receivedByUserId, receivedDate,
                supplierId: SupplierId, purchaseOrderId: Id, notes: $"Recepción de {Code}".TrimEnd()))
            .ToList();

        Status = PurchaseOrderStatus.Received;
        ReceivedAt = now;
        return movements;
    }
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>
/// Factura de compra (pantalla 05.4): lo que nos cobra un proveedor. "Vencida" no se guarda:
/// es una factura pendiente cuyo vencimiento ya pasó.
/// </summary>
public class PurchaseInvoice
{
    public int Id { get; set; }
    public string SupplierInvoiceNumber { get; private set; } = null!; // FC-9921, lo da el proveedor
    public int SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public int? PurchaseOrderId { get; private set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public string? Description { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Total { get; private set; }
    public PurchaseInvoiceStatus Status { get; private set; } = PurchaseInvoiceStatus.Pending;
    public DateTime? PaidAt { get; private set; }

    private PurchaseInvoice() { }

    public static PurchaseInvoice Create(Supplier supplier, string number, DateOnly issueDate, DateOnly dueDate,
        decimal total, PurchaseOrder? order, string? description)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new InvalidPurchaseInvoiceException("Escribe el número de la factura del proveedor.");
        if (total <= 0) throw new InvalidAmountException();
        if (dueDate < issueDate)
            throw new InvalidPurchaseInvoiceException("La fecha de vencimiento no puede ser anterior a la de emisión.");
        if (order is not null)
        {
            if (order.SupplierId != supplier.Id)
                throw new InvalidPurchaseInvoiceException($"La orden {order.Code} es de otro proveedor.");
            if (order.Status == PurchaseOrderStatus.Cancelled)
                throw new InvalidPurchaseInvoiceException("No se puede facturar una orden cancelada.");
        }

        return new PurchaseInvoice
        {
            SupplierInvoiceNumber = number.Trim().ToUpperInvariant(),
            SupplierId = supplier.Id,
            PurchaseOrderId = order?.Id,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IssueDate = issueDate,
            DueDate = dueDate,
            Total = total,
        };
    }

    public bool IsOverdue(DateOnly today) => Status == PurchaseInvoiceStatus.Pending && DueDate < today;

    public void Pay(DateTime now)
    {
        if (Status == PurchaseInvoiceStatus.Paid) throw new InvalidPurchaseInvoiceException("La factura ya está pagada.");
        Status = PurchaseInvoiceStatus.Paid;
        PaidAt = now;
    }
}
