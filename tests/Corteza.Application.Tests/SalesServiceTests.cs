using Corteza.Application.Features.Sales;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Application.Tests;

public class SalesServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly OrderService _orders;
    private readonly SalesInvoiceService _invoices;

    public SalesServiceTests()
    {
        _orders = new OrderService(_db.Context, _db.CurrentUser.Object, _db.Time);
        _invoices = new SalesInvoiceService(_db.Context, _db.Time);
    }

    public void Dispose() => _db.Dispose();

    private async Task<OrderDetail> DineInAsync(int tableId)
    {
        var bread = await _db.AddProductAsync("Pan francés", 1_500);
        var coffee = await _db.AddProductAsync("Café con leche", 3_800);
        return await _orders.CreateAsync(new OrderCreateRequest
        {
            ServiceType = ServiceType.DineIn,
            DiningTableId = tableId,
            Items = [new OrderItemRequest { ProductId = bread.Id, Quantity = 4 }, new OrderItemRequest { ProductId = coffee.Id, Quantity = 2 }],
        }, default);
    }

    [Fact(DisplayName = "Tomar un pedido en mesa lo guarda con el total y ocupa la mesa")]
    public async Task Create_occupies_table()
    {
        var table = await _db.AddTableAsync(4);

        var order = await DineInAsync(table.Id);

        Assert.Equal(13_600, order.Total);
        Assert.Equal(OrderStatus.Received, order.Status);
        var tables = await _orders.TablesAsync(default);
        Assert.Equal(TableState.Occupied, tables.Single().State);
        Assert.Equal(order.Id, tables.Single().OrderId);
    }

    [Fact(DisplayName = "Una mesa ocupada no recibe otro pedido y no se guarda nada")]
    public async Task Create_rejects_busy_table()
    {
        var table = await _db.AddTableAsync(4);
        await DineInAsync(table.Id);
        var bread = await _db.AddProductAsync("Croissant", 4_500);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<TableOccupiedException>(() => _orders.CreateAsync(new OrderCreateRequest
        {
            ServiceType = ServiceType.DineIn,
            DiningTableId = table.Id,
            Items = [new OrderItemRequest { ProductId = bread.Id, Quantity = 1 }],
        }, default));
        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "Cobrar en efectivo emite la factura con el cambio y libera la mesa")]
    public async Task Checkout_frees_table()
    {
        var table = await _db.AddTableAsync(4);
        var order = await DineInAsync(table.Id);
        await _orders.RequestBillAsync(order.Id, default);
        Assert.Equal(TableState.AwaitingPayment, (await _orders.TablesAsync(default)).Single().State);

        var invoice = await _orders.CheckoutAsync(order.Id, new CheckoutRequest { PaymentMethod = PaymentMethod.Cash, AmountReceived = 20_000 }, default);

        Assert.Equal(6_400, invoice.ChangeGiven);
        Assert.Equal(SalesInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(TableState.Free, (await _orders.TablesAsync(default)).Single().State);
    }

    [Fact(DisplayName = "Si el efectivo no alcanza no se guarda la factura")]
    public async Task Checkout_insufficient_cash_saves_nothing()
    {
        var table = await _db.AddTableAsync(4);
        var order = await DineInAsync(table.Id);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<InsufficientCashException>(() =>
            _orders.CheckoutAsync(order.Id, new CheckoutRequest { PaymentMethod = PaymentMethod.Cash, AmountReceived = 5_000 }, default));

        Assert.Equal(0, _db.Saves.Count);
        await using var fresh = _db.Fresh();
        Assert.Empty(fresh.SalesInvoices);
    }

    [Fact(DisplayName = "El pedido avanza de estado y anular la factura exige motivo")]
    public async Task Advance_and_void()
    {
        var table = await _db.AddTableAsync(2);
        var order = await DineInAsync(table.Id);

        var preparing = await _orders.AdvanceAsync(order.Id, new AdvanceOrderRequest { Status = OrderStatus.Preparing }, default);
        Assert.Equal(OrderStatus.Preparing, preparing.Status);

        var invoice = await _orders.CheckoutAsync(order.Id, new CheckoutRequest { PaymentMethod = PaymentMethod.Card }, default);
        var voided = await _invoices.VoidAsync(invoice.Id, new VoidSalesInvoiceRequest { Reason = "Error de cobro" }, default);

        Assert.Equal(SalesInvoiceStatus.Voided, voided.Status);
        Assert.Equal("Error de cobro", voided.VoidReason);
    }
}
