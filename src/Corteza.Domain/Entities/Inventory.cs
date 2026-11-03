using Corteza.Domain.Enums;

namespace Corteza.Domain.Entities;

public class SupplyCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!; // Harinas, Lácteos, Huevos...

    public ICollection<Supply> Supplies { get; set; } = [];
    public ICollection<SupplierSupplyCategory> SupplierLinks { get; set; } = [];
}

public class Supply
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public SupplyCategory Category { get; set; } = null!;
    public string PurchaseUnit { get; set; } = null!; // "Bulto de 50 kg"
    public string UnitLabel { get; set; } = null!;    // "bultos"
    public decimal? UnitSizeKg { get; set; }          // 50 → "1.250 kg en total"
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public int? MainSupplierId { get; set; }
    public Supplier? MainSupplier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = [];

    public bool IsOutOfStock => CurrentStock <= 0;
    public bool IsLowStock => !IsOutOfStock && CurrentStock < MinStock;
}

public class InventoryMovement
{
    public long Id { get; set; }
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }   // siempre positiva; el tipo dice si suma o resta
    public decimal StockAfter { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public DateOnly MovementDate { get; set; }
    public string? Location { get; set; }   // "Pasillo A - Tarima 04"
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
