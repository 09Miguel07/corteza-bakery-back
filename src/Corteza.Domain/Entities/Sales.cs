using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ProductCategory Category { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DiningTable
{
    public int Id { get; set; }
    public short Number { get; set; }
    public short Capacity { get; set; }
    public bool IsActive { get; set; } = true;

    // El estado (Libre / Ocupada / Por cobrar) no se guarda: se deriva del pedido abierto (Order.StateOf).
    public ICollection<Order> Orders { get; set; } = [];
}

/// <summary>Producto y cantidad que pide el cliente.</summary>
public readonly record struct OrderLine(Product Product, short Quantity);

/// <summary>
/// Pedido (P-0152). Flujo de cocina: Recibido → En preparación → Listo → Entregado.
/// Se cierra al cobrar (emite la factura) o al cancelar; cerrar libera la mesa.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // P-0152, lo genera una secuencia
    public int? CustomerId { get; private set; } // null = cliente ocasional
    public Customer? Customer { get; set; }
    public ServiceType ServiceType { get; private set; }
    public int? DiningTableId { get; private set; }
    public DiningTable? DiningTable { get; set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Received;
    public int ServedByUserId { get; private set; }
    public User ServedBy { get; set; } = null!;
    public decimal Total { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PreparingAt { get; private set; }
    public DateTime? ReadyAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? BillRequestedAt { get; private set; } // mesa "Por cobrar"
    public DateTime? ClosedAt { get; private set; }        // se cierra al cobrar o cancelar; libera la mesa

    public ICollection<OrderItem> Items { get; set; } = [];
    public SalesInvoice? Invoice { get; set; }

    private Order() { }

    public bool IsClosed => ClosedAt is not null;

    public static Order Create(ServiceType serviceType, DiningTable? table, bool tableHasOpenOrder, Customer? customer,
        IReadOnlyList<OrderLine> lines, int userId, DateTime now)
    {
        if (lines.Count == 0) throw new InvalidOrderException("Agrega al menos un producto al pedido.");
        if (serviceType == ServiceType.DineIn && table is null) throw new InvalidOrderException("Elige la mesa del pedido.");
        if (serviceType == ServiceType.Takeaway && table is not null) throw new InvalidOrderException("Un pedido para llevar no lleva mesa.");
        if (table is { IsActive: false }) throw new InvalidOrderException($"La mesa {table.Number} no está disponible.");
        if (table is not null && tableHasOpenOrder) throw new TableOccupiedException(table.Number);
        if (customer is { IsActive: false }) throw new InvalidOrderException("El cliente está inactivo.");

        var order = new Order
        {
            ServiceType = serviceType,
            DiningTableId = table?.Id,
            CustomerId = customer?.Id,
            ServedByUserId = userId,
            CreatedAt = now,
        };

        foreach (var group in lines.GroupBy(l => l.Product.Id))
        {
            var product = group.First().Product;
            if (group.Any(l => l.Quantity <= 0)) throw new InvalidQuantityException();
            if (!product.IsActive) throw new InvalidOrderException($"{product.Name} no está a la venta.");
            order.Items.Add(OrderItem.For(product, (short)group.Sum(l => l.Quantity)));
        }

        order.Total = order.Items.Sum(i => i.LineTotal);
        return order;
    }

    private static readonly Dictionary<OrderStatus, OrderStatus> Next = new()
    {
        [OrderStatus.Received] = OrderStatus.Preparing,
        [OrderStatus.Preparing] = OrderStatus.Ready,
        [OrderStatus.Ready] = OrderStatus.Delivered,
    };

    public void AdvanceTo(OrderStatus next, DateTime now)
    {
        if (!Next.TryGetValue(Status, out var allowed) || allowed != next)
            throw new InvalidOrderStateException($"Un pedido {Label(Status)} no puede pasar a {Label(next)}.");

        Status = next;
        switch (next)
        {
            case OrderStatus.Preparing: PreparingAt = now; break;
            case OrderStatus.Ready: ReadyAt = now; break;
            case OrderStatus.Delivered: DeliveredAt = now; break;
        }
    }

    public void Cancel(DateTime now)
    {
        if (Invoice is not null || IsClosed) throw new InvalidOrderStateException("No se puede cancelar un pedido ya cobrado o cerrado.");
        if (Status == OrderStatus.Delivered) throw new InvalidOrderStateException("No se puede cancelar un pedido ya entregado.");
        Status = OrderStatus.Cancelled;
        ClosedAt = now;
    }

    public void RequestBill(DateTime now)
    {
        if (ServiceType != ServiceType.DineIn) throw new InvalidOrderException("Solo los pedidos en mesa piden la cuenta.");
        if (IsClosed) throw new InvalidOrderStateException("El pedido ya está cerrado.");
        BillRequestedAt ??= now;
    }

    /// <summary>Cobra el pedido: emite la factura de venta, calcula el cambio y cierra el pedido (libera la mesa).</summary>
    public SalesInvoice Checkout(PaymentMethod method, decimal? amountReceived, int userId, DateTime now)
    {
        if (Status == OrderStatus.Cancelled) throw new InvalidOrderStateException("No se puede cobrar un pedido cancelado.");
        if (IsClosed || Invoice is not null) throw new InvalidOrderStateException("El pedido ya fue cobrado.");

        decimal? received = null, change = null;
        if (method == PaymentMethod.Cash)
        {
            if (amountReceived is null || amountReceived < Total) throw new InsufficientCashException(Total, amountReceived ?? 0);
            received = amountReceived;
            change = amountReceived - Total;
        }

        Invoice = SalesInvoice.Issue(this, method, received, change, userId, now);
        ClosedAt = now;
        return Invoice;
    }

    /// <summary>Estado de una mesa a partir de su pedido abierto (null = no tiene).</summary>
    public static TableState StateOf(Order? openOrder) =>
        openOrder is null || openOrder.IsClosed ? TableState.Free
        : openOrder.BillRequestedAt is not null ? TableState.AwaitingPayment
        : TableState.Occupied;

    private static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Received => "recibido",
        OrderStatus.Preparing => "en preparación",
        OrderStatus.Ready => "listo",
        OrderStatus.Delivered => "entregado",
        _ => "cancelado",
    };
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public short Quantity { get; private set; }
    public decimal UnitPrice { get; private set; } // copia del precio al momento de vender
    public decimal LineTotal { get; private set; } // columna calculada en SQL Server; el dominio la llena igual

    private OrderItem() { }

    internal static OrderItem For(Product product, short quantity) => new()
    {
        ProductId = product.Id,
        Quantity = quantity,
        UnitPrice = product.Price,
        LineTotal = quantity * product.Price,
    };
}

/// <summary>Factura de venta (FV-0231). Se emite al cobrar; solo se anula con motivo.</summary>
public class SalesInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = null!; // FV-0231, lo genera una secuencia
    public int OrderId { get; private set; }
    public Order Order { get; set; } = null!;
    public int? CustomerId { get; private set; }
    public Customer? Customer { get; set; }
    public decimal Total { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public decimal? AmountReceived { get; private set; }
    public decimal? ChangeGiven { get; private set; }
    public SalesInvoiceStatus Status { get; private set; } = SalesInvoiceStatus.Paid;
    public string? VoidReason { get; private set; }
    public int IssuedByUserId { get; private set; }
    public User IssuedBy { get; set; } = null!;
    public DateTime IssuedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; private set; }

    private SalesInvoice() { }

    internal static SalesInvoice Issue(Order order, PaymentMethod method, decimal? received, decimal? change, int userId, DateTime now) => new()
    {
        Order = order,
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        Total = order.Total,
        PaymentMethod = method,
        AmountReceived = received,
        ChangeGiven = change,
        IssuedByUserId = userId,
        IssuedAt = now,
    };

    public void Void(string reason, DateTime now)
    {
        if (Status == SalesInvoiceStatus.Voided) throw new InvalidSalesInvoiceException("La factura ya está anulada.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidSalesInvoiceException("Indica el motivo de la anulación.");
        Status = SalesInvoiceStatus.Voided;
        VoidReason = reason.Trim();
        VoidedAt = now;
    }
}
