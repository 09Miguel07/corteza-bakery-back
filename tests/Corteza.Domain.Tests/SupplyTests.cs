using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>
/// Taller 4 · Registro de insumos en inventario: stock inicial ≥ 0, categoría obligatoria
/// y no se retira más de lo disponible.
/// </summary>
public class SupplyTests
{
    private static Supply NewSupply(decimal initialStock = 0, decimal minStock = 20) =>
        Supply.Create("Harina de trigo", categoryId: 1, "Bulto de 50 kg", "bultos", unitSizeKg: 50,
            initialStock, minStock, mainSupplierId: null, TestData.UserId, TestData.Today);

    [Fact(DisplayName = "Un insumo nuevo sin cantidad inicial queda en cero y sin movimientos")]
    public void Create_without_initial_stock_starts_at_zero()
    {
        var supply = NewSupply();

        Assert.Equal(0, supply.CurrentStock);
        Assert.Empty(supply.Movements);
        Assert.True(supply.IsActive);
    }

    [Fact(DisplayName = "La cantidad inicial se registra como entrada para que el historial cuadre")]
    public void Create_with_initial_stock_records_an_entry()
    {
        var supply = NewSupply(initialStock: 65);

        Assert.Equal(65, supply.CurrentStock);
        var movement = Assert.Single(supply.Movements);
        Assert.Equal(MovementType.In, movement.Type);
        Assert.Equal(65, movement.StockAfter);
        Assert.Equal(TestData.UserId, movement.CreatedByUserId);
    }

    [Fact(DisplayName = "El stock inicial no puede ser negativo")]
    public void Create_rejects_negative_initial_stock()
    {
        Assert.Throws<InvalidQuantityException>(() => NewSupply(initialStock: -1));
    }

    [Fact(DisplayName = "La cantidad mínima no puede ser negativa")]
    public void Create_rejects_negative_min_stock()
    {
        Assert.Throws<InvalidQuantityException>(() => NewSupply(minStock: -5));
    }

    [Fact(DisplayName = "La categoría es obligatoria")]
    public void Create_requires_category()
    {
        Assert.Throws<InvalidSupplyException>(() =>
            Supply.Create("Harina", categoryId: 0, "Bulto", "bultos", null, 0, 0, null, TestData.UserId, TestData.Today));
    }

    [Fact(DisplayName = "El nombre del insumo es obligatorio")]
    public void Create_requires_name()
    {
        Assert.Throws<InvalidSupplyException>(() =>
            Supply.Create(" ", categoryId: 1, "Bulto", "bultos", null, 0, 0, null, TestData.UserId, TestData.Today));
    }

    [Theory(DisplayName = "Retiro de inventario dentro del stock disponible")]
    [InlineData(10, 3, 7)]
    [InlineData(5, 5, 0)]
    [InlineData(40, 12.5, 27.5)]
    public void RegisterExit_reduces_stock(decimal stock, decimal withdraw, decimal expected)
    {
        var supply = NewSupply(initialStock: stock);

        var movement = supply.RegisterExit(withdraw, TestData.UserId, TestData.Today);

        Assert.Equal(expected, supply.CurrentStock);
        Assert.Equal(MovementType.Out, movement.Type);
        Assert.Equal(withdraw, movement.Quantity);
        Assert.Equal(expected, movement.StockAfter);
    }

    [Fact(DisplayName = "Retirar más de lo disponible lanza 'Stock insuficiente' y el stock no cambia")]
    public void RegisterExit_more_than_available_throws_and_keeps_stock()
    {
        var supply = NewSupply(initialStock: 2);
        var movementsBefore = supply.Movements.Count;

        var error = Assert.Throws<InsufficientStockException>(() => supply.RegisterExit(5, TestData.UserId, TestData.Today));

        Assert.StartsWith("Stock insuficiente", error.Message);
        Assert.Equal(2, supply.CurrentStock);
        Assert.Equal(movementsBefore, supply.Movements.Count);
    }

    [Fact(DisplayName = "Una entrada suma al stock y guarda proveedor, orden y ubicación")]
    public void RegisterEntry_adds_stock_and_traceability()
    {
        var supply = NewSupply(initialStock: 40);

        var movement = supply.RegisterEntry(25, TestData.UserId, TestData.Today,
            supplierId: 3, purchaseOrderId: 10, location: "Pasillo A - Tarima 04", expirationDate: new DateOnly(2027, 11, 1));

        Assert.Equal(65, supply.CurrentStock);
        Assert.Equal(MovementType.In, movement.Type);
        Assert.Equal(65, movement.StockAfter);
        Assert.Equal(3, movement.SupplierId);
        Assert.Equal(10, movement.PurchaseOrderId);
        Assert.Equal("Pasillo A - Tarima 04", movement.Location);
    }

    [Theory(DisplayName = "No se registran entradas ni salidas con cantidad cero o negativa")]
    [InlineData(0)]
    [InlineData(-3)]
    public void Movements_reject_non_positive_quantities(decimal quantity)
    {
        var supply = NewSupply(initialStock: 10);

        Assert.Throws<InvalidQuantityException>(() => supply.RegisterEntry(quantity, TestData.UserId, TestData.Today));
        Assert.Throws<InvalidQuantityException>(() => supply.RegisterExit(quantity, TestData.UserId, TestData.Today));
        Assert.Equal(10, supply.CurrentStock);
    }

    [Theory(DisplayName = "Estado de existencias: agotado, por acabarse o suficiente")]
    [InlineData(0, 10, StockStatus.Out)]
    [InlineData(5, 10, StockStatus.Low)]
    [InlineData(10, 10, StockStatus.Ok)]
    [InlineData(12, 10, StockStatus.Ok)]
    [InlineData(0, 0, StockStatus.Out)]
    public void StatusOf_classifies_stock(decimal current, decimal min, StockStatus expected)
    {
        Assert.Equal(expected, Supply.StatusOf(current, min));
    }

    [Fact(DisplayName = "Retirar todo deja el insumo agotado")]
    public void Withdrawing_everything_leaves_it_out_of_stock()
    {
        var supply = NewSupply(initialStock: 8, minStock: 15);
        Assert.Equal(StockStatus.Low, supply.Status);

        supply.RegisterExit(8, TestData.UserId, TestData.Today);

        Assert.Equal(StockStatus.Out, supply.Status);
    }
}
