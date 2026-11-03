using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

/// <summary>Línea de una solicitud: qué insumo y cuánto se necesita.</summary>
public readonly record struct RfqLine(int SupplyId, decimal Quantity);

/// <summary>
/// Solicitud de cotización (SC-001) que se envía a varios proveedores. Reglas del taller 2:
/// solo se cotiza mientras está abierta, cada proveedor cotiza una vez y todos los precios son mayores que cero.
/// Al elegir la ganadora se cierra y de ella sale la orden de compra.
/// </summary>
public class RequestForQuotation
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // SC-001, lo genera una secuencia
    public string? Description { get; private set; }
    public RfqStatus Status { get; private set; } = RfqStatus.Open;
    public DateOnly ResponseDeadline { get; private set; }
    public int CreatedByUserId { get; private set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; private set; }

    public ICollection<RequestForQuotationItem> Items { get; set; } = [];
    public ICollection<Quotation> Quotations { get; set; } = [];

    /// <summary>La oferta de menor total entre las recibidas (null si no hay).</summary>
    public Quotation? LowestQuotation =>
        Quotations.Where(q => q.Status != QuotationStatus.Rejected).OrderBy(q => q.Total).FirstOrDefault();

    private RequestForQuotation() { } // EF Core

    public static RequestForQuotation Create(IEnumerable<RfqLine> lines, DateOnly responseDeadline, DateOnly today,
        int createdByUserId, string? description = null)
    {
        var items = lines.ToList();
        if (items.Count == 0)
            throw new InvalidRequestForQuotationException("Agrega al menos un insumo a la solicitud.");
        if (items.Any(i => i.Quantity <= 0))
            throw new InvalidQuantityException();
        if (items.Select(i => i.SupplyId).Distinct().Count() != items.Count)
            throw new InvalidRequestForQuotationException("Un insumo no se puede repetir en la misma solicitud.");
        if (responseDeadline < today)
            throw new InvalidRequestForQuotationException("La fecha límite no puede ser anterior a hoy.");

        var request = new RequestForQuotation
        {
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ResponseDeadline = responseDeadline,
            CreatedByUserId = createdByUserId,
        };
        foreach (var line in items)
            request.Items.Add(new RequestForQuotationItem { SupplyId = line.SupplyId, Quantity = line.Quantity });
        return request;
    }

    /// <summary>Registra la oferta de un proveedor: un precio unitario por cada insumo de la solicitud.</summary>
    public Quotation RegisterQuotation(Supplier supplier, IReadOnlyDictionary<int, decimal> unitPrices, int deliveryDays,
        DateTime now, string? notes = null)
    {
        EnsureOpen("No se puede cotizar");
        if (!supplier.IsActive)
            throw new InactiveSupplierException(supplier.Name);
        if (Quotations.Any(q => q.SupplierId == supplier.Id))
            throw new DuplicateQuotationException(supplier.Name);
        if (deliveryDays < 0)
            throw new InvalidRequestForQuotationException("Los días de entrega no pueden ser negativos.");

        var quotation = new Quotation
        {
            RequestForQuotation = this,
            SupplierId = supplier.Id,
            Supplier = supplier,
            DeliveryDays = deliveryDays,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ReceivedAt = now,
        };

        foreach (var item in Items)
        {
            if (!unitPrices.TryGetValue(item.SupplyId, out var price) || price <= 0)
                throw new InvalidAmountException("Monto inválido: cada insumo debe tener un precio mayor que cero.");
            quotation.Items.Add(new QuotationItem { SupplyId = item.SupplyId, Quantity = item.Quantity, UnitPrice = price });
        }

        quotation.Total = quotation.Items.Sum(i => i.LineTotal);
        Quotations.Add(quotation);
        return quotation;
    }

    /// <summary>Elige la oferta ganadora: las demás quedan rechazadas y la solicitud se cierra.</summary>
    public void SelectQuotation(Quotation quotation, DateTime now)
    {
        EnsureOpen("No se puede elegir una cotización");
        if (!Quotations.Contains(quotation))
            throw new InvalidRequestForQuotationException("La cotización no pertenece a esta solicitud.");

        foreach (var q in Quotations)
            q.Status = ReferenceEquals(q, quotation) ? QuotationStatus.Selected : QuotationStatus.Rejected;

        Status = RfqStatus.Closed;
        ClosedAt = now;
    }

    public void Cancel(DateTime now)
    {
        EnsureOpen("No se puede cancelar");
        Status = RfqStatus.Cancelled;
        ClosedAt = now;
    }

    private void EnsureOpen(string action)
    {
        if (Status != RfqStatus.Open)
            throw new QuotationNotAllowedException($"{action}: la solicitud {Code} no está abierta.".Replace("  ", " "));
    }
}

public class RequestForQuotationItem
{
    public int Id { get; set; }
    public int RequestForQuotationId { get; set; }
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    public decimal Quantity { get; set; }
}

/// <summary>Oferta de un proveedor para una solicitud. Queda RECIBIDA hasta que se elige la ganadora.</summary>
public class Quotation
{
    public int Id { get; set; }
    public int RequestForQuotationId { get; set; }
    public RequestForQuotation RequestForQuotation { get; set; } = null!;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public QuotationStatus Status { get; set; } = QuotationStatus.Received;
    public int DeliveryDays { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTime ReceivedAt { get; set; }

    public ICollection<QuotationItem> Items { get; set; } = [];
}

public class QuotationItem
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
