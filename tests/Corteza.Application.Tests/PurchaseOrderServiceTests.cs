using Corteza.Application.Common.Exceptions;
using Corteza.Application.Features.Purchasing;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Tests;

public class PurchaseOrderServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PurchaseOrderService _service;

    public PurchaseOrderServiceTests() =>
        _service = new PurchaseOrderService(_db.Context, _db.CurrentUser.Object, _db.Time);

    public void Dispose() => _db.Dispose();

    private PurchaseOrderCreateRequest Request(int supplierId, int supplyId, decimal quantity = 100, decimal price = 50_000) => new()
    {
        SupplierId = supplierId,
        ExpectedDeliveryDate = _db.Today.AddDays(2),
        Items = [new PurchaseOrderLineRequest { SupplyId = supplyId, Quantity = quantity, UnitPrice = price }],
    };

    [Fact(DisplayName = "Crear una orden la guarda PENDIENTE con su total")]
    public async Task Create_saves_pending_order()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync();

        var order = await _service.CreateAsync(Request(supplier.Id, flour.Id), default);

        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Equal(5_000_000, order.Total);
        Assert.Equal("Harina de trigo", Assert.Single(order.Items).SupplyName);
        Assert.Equal(1, _db.Saves.Count);
    }

    [Fact(DisplayName = "Crear con un insumo que no existe falla sin guardar")]
    public async Task Create_with_unknown_supply_does_not_save()
    {
        var supplier = await _db.AddSupplierAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(Request(supplier.Id, supplyId: 999), default));

        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "Crear con un proveedor inactivo falla sin guardar")]
    public async Task Create_with_inactive_supplier_does_not_save()
    {
        var supplier = await _db.AddSupplierAsync(active: false);
        var flour = await _db.AddSupplyAsync();

        await Assert.ThrowsAsync<InactiveSupplierException>(() => _service.CreateAsync(Request(supplier.Id, flour.Id), default));

        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "Aprobar dentro del presupuesto descuenta el presupuesto del proveedor en la base")]
    public async Task Approve_within_budget_persists_new_budget()
    {
        var supplier = await _db.AddSupplierAsync(budget: 10_000_000);
        var flour = await _db.AddSupplyAsync();
        var order = await _service.CreateAsync(Request(supplier.Id, flour.Id), default);

        var approved = await _service.ApproveAsync(order.Id, default);

        Assert.Equal(PurchaseOrderStatus.Approved, approved.Status);
        await using var fresh = _db.Fresh();
        Assert.Equal(5_000_000, (await fresh.Suppliers.SingleAsync(s => s.Id == supplier.Id)).AvailableBudget);
    }

    [Fact(DisplayName = "Aprobar por encima del presupuesto falla, no guarda y la orden sigue pendiente")]
    public async Task Approve_exceeding_budget_does_not_save()
    {
        var supplier = await _db.AddSupplierAsync(budget: 1_000_000);
        var flour = await _db.AddSupplyAsync();
        var order = await _service.CreateAsync(Request(supplier.Id, flour.Id), default);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<BudgetExceededException>(() => _service.ApproveAsync(order.Id, default));

        Assert.Equal(0, _db.Saves.Count);
        await using var fresh = _db.Fresh();
        Assert.Equal(PurchaseOrderStatus.Pending, (await fresh.PurchaseOrders.SingleAsync()).Status);
        Assert.Equal(1_000_000, (await fresh.Suppliers.SingleAsync()).AvailableBudget);
    }

    [Fact(DisplayName = "Recibir una orden aprobada suma al inventario y deja los movimientos")]
    public async Task Receive_adds_stock_and_movements()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync(stock: 40);
        var order = await _service.CreateAsync(Request(supplier.Id, flour.Id, quantity: 25, price: 50_000), default);
        await _service.ApproveAsync(order.Id, default);

        var received = await _service.ReceiveAsync(order.Id, new ReceivePurchaseOrderRequest(), default);

        Assert.Equal(PurchaseOrderStatus.Received, received.Status);
        await using var fresh = _db.Fresh();
        Assert.Equal(65, (await fresh.Supplies.SingleAsync()).CurrentStock);
        var entry = await fresh.InventoryMovements.Where(m => m.PurchaseOrderId == order.Id).SingleAsync();
        Assert.Equal(MovementType.In, entry.Type);
        Assert.Equal(supplier.Id, entry.SupplierId);
    }

    [Fact(DisplayName = "Cancelar una orden pendiente guarda el motivo")]
    public async Task Cancel_saves_reason()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync();
        var order = await _service.CreateAsync(Request(supplier.Id, flour.Id), default);

        var cancelled = await _service.CancelAsync(order.Id, new CancelPurchaseOrderRequest { Reason = "Pedido duplicado" }, default);

        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("Pedido duplicado", cancelled.CancelReason);
    }

    [Fact(DisplayName = "Aprobar una orden que no existe responde No encontrado")]
    public async Task Approve_unknown_order_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveAsync(404, default));
    }

    [Fact(DisplayName = "El resumen cuenta pendientes, aprobadas y las que llegan hoy")]
    public async Task Summary_counts_by_status()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync();
        var first = await _service.CreateAsync(Request(supplier.Id, flour.Id, quantity: 1), default);
        await _service.CreateAsync(Request(supplier.Id, flour.Id, quantity: 2), default);
        await _service.ApproveAsync(first.Id, default);

        var summary = await _service.SummaryAsync(default);

        Assert.Equal(1, summary.Pending);
        Assert.Equal(1, summary.Approved);
    }

    [Fact(DisplayName = "La lista filtra por estado")]
    public async Task List_filters_by_status()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync();
        var first = await _service.CreateAsync(Request(supplier.Id, flour.Id, quantity: 1), default);
        await _service.CreateAsync(Request(supplier.Id, flour.Id, quantity: 2), default);
        await _service.ApproveAsync(first.Id, default);

        var page = await _service.ListAsync(new PurchaseOrderQuery { Status = PurchaseOrderStatus.Approved }, default);

        Assert.Equal(1, page.Total);
        Assert.Equal(first.Id, Assert.Single(page.Items).Id);
        Assert.Contains("bultos de Harina de trigo", page.Items[0].ItemsSummary);
    }
}
