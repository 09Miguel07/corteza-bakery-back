using Corteza.Domain.Enums;

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

    // El estado (Libre / Ocupada / Por cobrar) no se guarda: se deriva del pedido abierto.
    public ICollection<Order> Orders { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // P-0152, lo genera una secuencia
    public int? CustomerId { get; set; }      // null = cliente ocasional
    public Customer? Customer { get; set; }
    public ServiceType ServiceType { get; set; }
    public int? DiningTableId { get; set; }
    public DiningTable? DiningTable { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Received;
    public int ServedByUserId { get; set; }
    public User ServedBy { get; set; } = null!;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PreparingAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? BillRequestedAt { get; set; } // mesa "Por cobrar"
    public DateTime? ClosedAt { get; set; }        // se cierra al cobrar o cancelar; libera la mesa

    public ICollection<OrderItem> Items { get; set; } = [];
    public SalesInvoice? Invoice { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public short Quantity { get; set; }
    public decimal UnitPrice { get; set; } // copia del precio al momento de vender
    public decimal LineTotal { get; private set; } // columna calculada
}

public class SalesInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = null!; // FV-0231, lo genera una secuencia
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal? AmountReceived { get; set; }
    public decimal? ChangeGiven { get; set; }
    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Paid;
    public string? VoidReason { get; set; }
    public int IssuedByUserId { get; set; }
    public User IssuedBy { get; set; } = null!;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; set; }
}
