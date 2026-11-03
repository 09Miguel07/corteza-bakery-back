using Corteza.Application.Features.Inventory;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Corteza.Application.Tests;

public class InventoryMovementServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InventoryMovementService _service;

    public InventoryMovementServiceTests() =>
        _service = new InventoryMovementService(_db.Context, _db.CurrentUser.Object, _db.Time);

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "Una entrada suma al stock y queda a nombre del usuario actual")]
    public async Task Entry_adds_stock_for_current_user()
    {
        var flour = await _db.AddSupplyAsync(stock: 40);

        var movement = await _service.RegisterAsync(
            new MovementCreateRequest { SupplyId = flour.Id, Type = MovementType.In, Quantity = 25 }, default);

        Assert.Equal(65, movement.StockAfter);
        _db.CurrentUser.Verify(u => u.RequireUserId(), Times.Once);
    }

    [Fact(DisplayName = "Retirar más de lo disponible: 'Stock insuficiente', el stock no cambia y no se guarda nada")]
    public async Task Exit_more_than_available_does_not_save()
    {
        var milk = await _db.AddSupplyAsync("Leche entera", stock: 2);

        var error = await Assert.ThrowsAsync<InsufficientStockException>(() => _service.RegisterAsync(
            new MovementCreateRequest { SupplyId = milk.Id, Type = MovementType.Out, Quantity = 5 }, default));

        Assert.StartsWith("Stock insuficiente", error.Message);
        Assert.Equal(0, _db.Saves.Count);
        await using var fresh = _db.Fresh();
        Assert.Equal(2, (await fresh.Supplies.SingleAsync()).CurrentStock);
        Assert.Equal(1, await fresh.InventoryMovements.CountAsync()); // solo la entrada inicial
    }

    [Fact(DisplayName = "No se registra un movimiento con fecha futura")]
    public async Task Future_date_is_rejected()
    {
        var flour = await _db.AddSupplyAsync();

        await Assert.ThrowsAsync<Common.Exceptions.BusinessRuleException>(() => _service.RegisterAsync(
            new MovementCreateRequest { SupplyId = flour.Id, Type = MovementType.In, Quantity = 1, MovementDate = _db.Today.AddDays(1) },
            default));
    }
}
