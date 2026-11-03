using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

public class SupplyCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!; // Harinas, Lácteos, Huevos...

    public ICollection<Supply> Supplies { get; set; } = [];
    public ICollection<SupplierSupplyCategory> SupplierLinks { get; set; } = [];
}

/// <summary>
/// Insumo de bodega. Las reglas del taller 4 viven aquí: stock inicial ≥ 0, categoría obligatoria
/// y una salida nunca deja el stock en negativo. La existencia solo cambia con movimientos.
/// </summary>
public class Supply
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // INS-001, lo genera una secuencia
    public string Name { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public SupplyCategory Category { get; set; } = null!;
    public string PurchaseUnit { get; private set; } = null!; // "Bulto de 50 kg"
    public string UnitLabel { get; private set; } = null!;    // "bultos"
    public decimal? UnitSizeKg { get; private set; }          // 50 → "1.250 kg en total"
    public decimal CurrentStock { get; private set; }
    public decimal MinStock { get; private set; }
    public int? MainSupplierId { get; private set; }
    public Supplier? MainSupplier { get; set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Token de concurrencia: dos movimientos simultáneos no se pisan.</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = [];

    public StockStatus Status => StatusOf(CurrentStock, MinStock);

    private Supply() { } // EF Core

    public static Supply Create(string name, int categoryId, string purchaseUnit, string unitLabel, decimal? unitSizeKg,
        decimal initialStock, decimal minStock, int? mainSupplierId, int createdByUserId, DateOnly today)
    {
        if (initialStock < 0)
            throw new InvalidQuantityException("El stock inicial no puede ser negativo.");

        var supply = new Supply();
        supply.UpdateDetails(name, categoryId, purchaseUnit, unitLabel, unitSizeKg, minStock, mainSupplierId);

        // El stock inicial queda como entrada para que el historial cuadre con la existencia.
        if (initialStock > 0)
            supply.RegisterEntry(initialStock, createdByUserId, today, supplierId: mainSupplierId,
                notes: "Stock inicial al registrar el insumo");

        return supply;
    }

    public void UpdateDetails(string name, int categoryId, string purchaseUnit, string unitLabel, decimal? unitSizeKg,
        decimal minStock, int? mainSupplierId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidSupplyException("El nombre del insumo es obligatorio.");
        if (categoryId <= 0)
            throw new InvalidSupplyException("La categoría es obligatoria.");
        if (string.IsNullOrWhiteSpace(purchaseUnit) || string.IsNullOrWhiteSpace(unitLabel))
            throw new InvalidSupplyException("La unidad de compra y cómo se cuenta son obligatorias.");
        if (unitSizeKg is <= 0)
            throw new InvalidQuantityException("El peso por unidad debe ser mayor que cero.");
        if (minStock < 0)
            throw new InvalidQuantityException("La cantidad mínima no puede ser negativa.");

        Name = name.Trim();
        CategoryId = categoryId;
        PurchaseUnit = purchaseUnit.Trim();
        UnitLabel = unitLabel.Trim().ToLowerInvariant();
        UnitSizeKg = unitSizeKg;
        MinStock = minStock;
        MainSupplierId = mainSupplierId;
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Entrada a bodega: compra recibida, devolución o stock inicial.</summary>
    public InventoryMovement RegisterEntry(decimal quantity, int userId, DateOnly date, int? supplierId = null,
        int? purchaseOrderId = null, string? location = null, DateOnly? expirationDate = null, string? notes = null)
    {
        EnsurePositive(quantity);
        CurrentStock += quantity;
        return AddMovement(MovementType.In, quantity, userId, date, supplierId, purchaseOrderId, location, expirationDate, notes);
    }

    /// <summary>Salida de bodega. Si no alcanza, lanza "Stock insuficiente" y no cambia nada.</summary>
    public InventoryMovement RegisterExit(decimal quantity, int userId, DateOnly date, string? notes = null)
    {
        EnsurePositive(quantity);
        if (quantity > CurrentStock)
            throw new InsufficientStockException(CurrentStock, quantity, UnitLabel, Name);

        CurrentStock -= quantity;
        return AddMovement(MovementType.Out, quantity, userId, date, null, null, null, null, notes);
    }

    public static StockStatus StatusOf(decimal current, decimal min) =>
        current <= 0 ? StockStatus.Out : current < min ? StockStatus.Low : StockStatus.Ok;

    private static void EnsurePositive(decimal quantity)
    {
        if (quantity <= 0) throw new InvalidQuantityException();
    }

    private InventoryMovement AddMovement(MovementType type, decimal quantity, int userId, DateOnly date, int? supplierId,
        int? purchaseOrderId, string? location, DateOnly? expirationDate, string? notes)
    {
        var movement = new InventoryMovement
        {
            Supply = this,
            SupplyId = Id,
            Type = type,
            Quantity = quantity,
            StockAfter = CurrentStock,
            SupplierId = supplierId,
            PurchaseOrderId = purchaseOrderId,
            MovementDate = date,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            ExpirationDate = expirationDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = userId,
        };
        Movements.Add(movement);
        return movement;
    }
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
