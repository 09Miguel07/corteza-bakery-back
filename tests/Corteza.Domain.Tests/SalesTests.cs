using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>Módulo 02 · Facturación y Ventas: pedido, mesa, estados, cobro y factura de venta.</summary>
public class SalesTests
{
    private static readonly Product Bread = new() { Id = 1, Name = "Pan francés", Price = 1_500 };
    private static readonly Product Coffee = new() { Id = 2, Name = "Café con leche", Price = 3_800 };
    private static DiningTable Table(short number = 4) => new() { Id = number, Number = number, Capacity = 4 };

    private static Order DineIn(DiningTable? table = null, bool busy = false, params OrderLine[] lines) =>
        Order.Create(ServiceType.DineIn, table ?? Table(), busy, null,
            lines.Length > 0 ? lines : [new OrderLine(Bread, 4), new OrderLine(Coffee, 2)], TestData.UserId, TestData.Now);

    private static Order Takeaway(params OrderLine[] lines) =>
        Order.Create(ServiceType.Takeaway, null, false, null,
            lines.Length > 0 ? lines : [new OrderLine(Bread, 4), new OrderLine(Coffee, 2)], TestData.UserId, TestData.Now);

    [Fact(DisplayName = "Un pedido nuevo queda RECIBIDO con el total calculado")]
    public void Create_computes_total()
    {
        var order = DineIn();

        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal(13_600, order.Total); // 4 × 1.500 + 2 × 3.800
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(6_000, order.Items.First().LineTotal);
    }

    [Fact(DisplayName = "El mismo producto dos veces se junta en una sola línea")]
    public void Create_merges_repeated_products()
    {
        var order = Takeaway(new OrderLine(Bread, 2), new OrderLine(Bread, 3));

        Assert.Single(order.Items);
        Assert.Equal(5, order.Items.Single().Quantity);
        Assert.Equal(7_500, order.Total);
    }

    [Fact(DisplayName = "Un pedido sin productos no se crea")]
    public void Create_requires_lines()
    {
        Assert.Throws<InvalidOrderException>(() =>
            Order.Create(ServiceType.Takeaway, null, false, null, [], TestData.UserId, TestData.Now));
    }

    [Theory(DisplayName = "La cantidad de cada producto debe ser mayor que cero")]
    [InlineData(0)]
    [InlineData(-2)]
    public void Create_rejects_bad_quantity(short quantity)
    {
        Assert.Throws<InvalidQuantityException>(() => Takeaway(new OrderLine(Bread, quantity)));
    }

    [Fact(DisplayName = "Un producto que no está a la venta no se pide")]
    public void Create_rejects_inactive_product()
    {
        var old = new Product { Id = 9, Name = "Roscón", Price = 9_000, IsActive = false };

        var error = Assert.Throws<InvalidOrderException>(() => Takeaway(new OrderLine(old, 1)));
        Assert.Contains("Roscón", error.Message);
    }

    [Fact(DisplayName = "Un pedido en mesa necesita mesa y uno para llevar no la lleva")]
    public void Create_checks_table_by_service_type()
    {
        Assert.Throws<InvalidOrderException>(() =>
            Order.Create(ServiceType.DineIn, null, false, null, [new OrderLine(Bread, 1)], TestData.UserId, TestData.Now));
        Assert.Throws<InvalidOrderException>(() =>
            Order.Create(ServiceType.Takeaway, Table(), false, null, [new OrderLine(Bread, 1)], TestData.UserId, TestData.Now));
    }

    [Fact(DisplayName = "Una mesa ocupada no recibe otro pedido")]
    public void Create_rejects_busy_table()
    {
        var error = Assert.Throws<TableOccupiedException>(() => DineIn(Table(4), busy: true));
        Assert.Equal("La mesa 4 ya tiene un pedido abierto.", error.Message);
    }

    [Fact(DisplayName = "El pedido avanza Recibido → En preparación → Listo → Entregado")]
    public void AdvanceTo_follows_kitchen_flow()
    {
        var order = DineIn();

        order.AdvanceTo(OrderStatus.Preparing, TestData.Now);
        order.AdvanceTo(OrderStatus.Ready, TestData.Now);
        order.AdvanceTo(OrderStatus.Delivered, TestData.Now);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.NotNull(order.PreparingAt);
        Assert.NotNull(order.ReadyAt);
        Assert.NotNull(order.DeliveredAt);
    }

    [Fact(DisplayName = "No se salta pasos ni se devuelve el estado")]
    public void AdvanceTo_rejects_skips()
    {
        var order = DineIn();

        Assert.Throws<InvalidOrderStateException>(() => order.AdvanceTo(OrderStatus.Delivered, TestData.Now));
        order.AdvanceTo(OrderStatus.Preparing, TestData.Now);
        Assert.Throws<InvalidOrderStateException>(() => order.AdvanceTo(OrderStatus.Received, TestData.Now));
    }

    [Fact(DisplayName = "Cancelar cierra el pedido y libera la mesa")]
    public void Cancel_closes_order()
    {
        var order = DineIn();

        order.Cancel(TestData.Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.ClosedAt);
        Assert.Equal(TableState.Free, Order.StateOf(null));
    }

    [Fact(DisplayName = "Un pedido entregado o cobrado no se cancela")]
    public void Cancel_rejects_delivered_or_paid()
    {
        var paid = Takeaway();
        paid.Checkout(PaymentMethod.Card, null, TestData.UserId, TestData.Now);
        Assert.Throws<InvalidOrderStateException>(() => paid.Cancel(TestData.Now));

        var delivered = DineIn();
        delivered.AdvanceTo(OrderStatus.Preparing, TestData.Now);
        delivered.AdvanceTo(OrderStatus.Ready, TestData.Now);
        delivered.AdvanceTo(OrderStatus.Delivered, TestData.Now);
        Assert.Throws<InvalidOrderStateException>(() => delivered.Cancel(TestData.Now));
    }

    [Fact(DisplayName = "Pedir la cuenta deja la mesa POR COBRAR")]
    public void RequestBill_marks_table_awaiting_payment()
    {
        var order = DineIn();
        Assert.Equal(TableState.Occupied, Order.StateOf(order));

        order.RequestBill(TestData.Now);

        Assert.Equal(TableState.AwaitingPayment, Order.StateOf(order));
    }

    [Fact(DisplayName = "Un pedido para llevar no pide la cuenta")]
    public void RequestBill_only_dine_in()
    {
        Assert.Throws<InvalidOrderException>(() => Takeaway().RequestBill(TestData.Now));
    }

    [Fact(DisplayName = "Cobrar en efectivo calcula el cambio, emite la factura y cierra el pedido")]
    public void Checkout_cash_gives_change()
    {
        var order = DineIn();

        var invoice = order.Checkout(PaymentMethod.Cash, 20_000, TestData.UserId, TestData.Now);

        Assert.Equal(13_600, invoice.Total);
        Assert.Equal(20_000, invoice.AmountReceived);
        Assert.Equal(6_400, invoice.ChangeGiven);
        Assert.Equal(SalesInvoiceStatus.Paid, invoice.Status);
        Assert.Same(invoice, order.Invoice);
        Assert.NotNull(order.ClosedAt);
    }

    [Fact(DisplayName = "El efectivo exacto no deja cambio")]
    public void Checkout_exact_cash()
    {
        var invoice = DineIn().Checkout(PaymentMethod.Cash, 13_600, TestData.UserId, TestData.Now);
        Assert.Equal(0, invoice.ChangeGiven);
    }

    [Fact(DisplayName = "Si el efectivo no alcanza, no se cobra y el pedido sigue abierto")]
    public void Checkout_rejects_insufficient_cash()
    {
        var order = DineIn();

        var error = Assert.Throws<InsufficientCashException>(() =>
            order.Checkout(PaymentMethod.Cash, 10_000, TestData.UserId, TestData.Now));

        Assert.Equal("Efectivo insuficiente: el pedido vale $13.600 y se recibieron $10.000.", error.Message);
        Assert.Null(order.Invoice);
        Assert.Null(order.ClosedAt);
    }

    [Fact(DisplayName = "Con tarjeta o transferencia no se registra efectivo ni cambio")]
    public void Checkout_card_has_no_change()
    {
        var invoice = Takeaway().Checkout(PaymentMethod.Transfer, 50_000, TestData.UserId, TestData.Now);

        Assert.Null(invoice.AmountReceived);
        Assert.Null(invoice.ChangeGiven);
    }

    [Fact(DisplayName = "No se cobra dos veces ni un pedido cancelado")]
    public void Checkout_rejects_closed_orders()
    {
        var paid = Takeaway();
        paid.Checkout(PaymentMethod.Card, null, TestData.UserId, TestData.Now);
        Assert.Throws<InvalidOrderStateException>(() => paid.Checkout(PaymentMethod.Card, null, TestData.UserId, TestData.Now));

        var cancelled = Takeaway();
        cancelled.Cancel(TestData.Now);
        Assert.Throws<InvalidOrderStateException>(() => cancelled.Checkout(PaymentMethod.Card, null, TestData.UserId, TestData.Now));
    }

    [Fact(DisplayName = "Anular una factura exige motivo y solo se anula una vez")]
    public void Void_requires_reason_once()
    {
        var invoice = Takeaway().Checkout(PaymentMethod.Card, null, TestData.UserId, TestData.Now);

        Assert.Throws<InvalidSalesInvoiceException>(() => invoice.Void(" ", TestData.Now));
        invoice.Void("Cliente devolvió el producto", TestData.Now);
        Assert.Equal(SalesInvoiceStatus.Voided, invoice.Status);
        Assert.Throws<InvalidSalesInvoiceException>(() => invoice.Void("Otra vez", TestData.Now));
    }
}
