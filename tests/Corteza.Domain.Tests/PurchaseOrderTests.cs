using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>
/// Taller 3 · Aprobación de órdenes de compra: no se aprueba una orden que excede el presupuesto del proveedor,
/// una orden aprobada no se vuelve a aprobar, la aprobación descuenta el presupuesto y solo se cancela
/// una orden pendiente indicando el motivo.
/// </summary>
public class PurchaseOrderTests
{
    private const int Flour = 1;
    private const int Sugar = 2;

    private static PurchaseOrder NewOrder(Supplier supplier, params PurchaseOrderLine[] lines) =>
        PurchaseOrder.Create(supplier,
            lines.Length > 0 ? lines : [new PurchaseOrderLine(Flour, 25, 50_000), new PurchaseOrderLine(Sugar, 10, 45_000)],
            expectedDeliveryDate: TestData.Today.AddDays(2), TestData.Today, TestData.UserId);

    /// <summary>Orden de $5.000.000: 100 bultos a $50.000.</summary>
    private static PurchaseOrder FiveMillionOrder(Supplier supplier) =>
        NewOrder(supplier, new PurchaseOrderLine(Flour, 100, 50_000));

    private static Supply NewSupply(decimal stock) =>
        Supply.Create("Harina de trigo", 1, "Bulto de 50 kg", "bultos", 50, stock, 20, null, TestData.UserId, TestData.Today);

    [Fact(DisplayName = "Una orden nueva queda PENDIENTE con el total calculado")]
    public void Create_starts_pending_with_total()
    {
        var order = NewOrder(TestData.Supplier());

        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Equal(1_700_000, order.Total);
        Assert.Equal(2, order.Items.Count);
        Assert.True(order.IsActive);
    }

    [Fact(DisplayName = "No se crea una orden para un proveedor inactivo")]
    public void Create_rejects_inactive_supplier()
    {
        Assert.Throws<InactiveSupplierException>(() => NewOrder(TestData.Supplier(active: false)));
    }

    [Fact(DisplayName = "Una orden necesita al menos un insumo")]
    public void Create_requires_lines()
    {
        Assert.Throws<InvalidPurchaseOrderException>(() =>
            PurchaseOrder.Create(TestData.Supplier(), [], TestData.Today, TestData.Today, TestData.UserId));
    }

    [Theory(DisplayName = "Cantidad y precio deben ser mayores que cero")]
    [InlineData(0, 50_000, typeof(InvalidQuantityException))]
    [InlineData(-1, 50_000, typeof(InvalidQuantityException))]
    [InlineData(10, 0, typeof(InvalidAmountException))]
    [InlineData(10, -5, typeof(InvalidAmountException))]
    public void Create_rejects_invalid_quantity_or_price(decimal quantity, decimal price, Type expected)
    {
        var error = Record(() => NewOrder(TestData.Supplier(), new PurchaseOrderLine(Flour, quantity, price)));

        Assert.NotNull(error);
        Assert.Equal(expected, error!.GetType());
    }

    [Fact(DisplayName = "Un insumo no se repite en la orden")]
    public void Create_rejects_duplicated_supplies()
    {
        Assert.Throws<InvalidPurchaseOrderException>(() =>
            NewOrder(TestData.Supplier(), new PurchaseOrderLine(Flour, 5, 1_000), new PurchaseOrderLine(Flour, 2, 1_000)));
    }

    [Fact(DisplayName = "La fecha de entrega no puede ser anterior a hoy")]
    public void Create_rejects_past_delivery_date()
    {
        Assert.Throws<InvalidPurchaseOrderException>(() =>
            PurchaseOrder.Create(TestData.Supplier(), [new PurchaseOrderLine(Flour, 1, 1_000)],
                TestData.Today.AddDays(-1), TestData.Today, TestData.UserId));
    }

    [Fact(DisplayName = "Aprobar dentro del presupuesto: queda APROBADA y el presupuesto baja a $5.000.000")]
    public void Approve_within_budget_reduces_supplier_budget()
    {
        var supplier = TestData.Supplier(budget: 10_000_000);
        var order = FiveMillionOrder(supplier);

        order.Approve(supplier, TestData.UserId, TestData.Now);

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        Assert.Equal(5_000_000, supplier.AvailableBudget);
        Assert.Equal(TestData.Now, order.ApprovedAt);
        Assert.Equal(TestData.UserId, order.ApprovedByUserId);
    }

    [Fact(DisplayName = "No se aprueba una orden que excede el presupuesto y nada cambia")]
    public void Approve_exceeding_budget_throws_and_keeps_pending()
    {
        var supplier = TestData.Supplier(budget: 1_000_000);
        var order = FiveMillionOrder(supplier);

        var error = Assert.Throws<BudgetExceededException>(() => order.Approve(supplier, TestData.UserId, TestData.Now));

        Assert.StartsWith("Presupuesto insuficiente", error.Message);
        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Equal(1_000_000, supplier.AvailableBudget);
    }

    [Fact(DisplayName = "Una orden aprobada no se vuelve a aprobar ni descuenta dos veces")]
    public void Approve_twice_is_not_allowed()
    {
        var supplier = TestData.Supplier(budget: 10_000_000);
        var order = FiveMillionOrder(supplier);
        order.Approve(supplier, TestData.UserId, TestData.Now);

        var error = Assert.Throws<InvalidOrderStateException>(() => order.Approve(supplier, TestData.UserId, TestData.Now));

        Assert.Contains("ya está aprobada", error.Message);
        Assert.Equal(5_000_000, supplier.AvailableBudget);
    }

    [Fact(DisplayName = "La orden se aprueba con el presupuesto de su propio proveedor")]
    public void Approve_with_another_supplier_is_rejected()
    {
        var order = FiveMillionOrder(TestData.Supplier(id: 1));

        Assert.Throws<InvalidPurchaseOrderException>(() =>
            order.Approve(TestData.Supplier(id: 2), TestData.UserId, TestData.Now));
    }

    [Fact(DisplayName = "Cancelar una orden pendiente guarda el motivo")]
    public void Cancel_pending_with_reason()
    {
        var order = NewOrder(TestData.Supplier());

        order.Cancel("El proveedor no tiene existencias", TestData.Now);

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Equal("El proveedor no tiene existencias", order.CancelReason);
        Assert.False(order.IsActive);
    }

    [Theory(DisplayName = "Cancelar exige un motivo")]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_requires_reason(string reason)
    {
        var order = NewOrder(TestData.Supplier());

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Cancel(reason, TestData.Now));
        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
    }

    [Fact(DisplayName = "Solo se cancela una orden pendiente")]
    public void Cancel_approved_order_is_not_allowed()
    {
        var supplier = TestData.Supplier();
        var order = NewOrder(supplier);
        order.Approve(supplier, TestData.UserId, TestData.Now);

        Assert.Throws<InvalidOrderStateException>(() => order.Cancel("Ya no se necesita", TestData.Now));
    }

    [Fact(DisplayName = "Recibir una orden aprobada suma al inventario y deja trazabilidad")]
    public void Receive_approved_order_adds_stock()
    {
        var supplier = TestData.Supplier(id: 3);
        var order = NewOrder(supplier, new PurchaseOrderLine(Flour, 25, 50_000));
        order.Id = 42;
        order.Approve(supplier, TestData.UserId, TestData.Now);
        var flour = NewSupply(stock: 40);

        var movements = order.Receive(new Dictionary<int, Supply> { [Flour] = flour }, TestData.UserId, TestData.Today, TestData.Now);

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(65, flour.CurrentStock);
        var movement = Assert.Single(movements);
        Assert.Equal(MovementType.In, movement.Type);
        Assert.Equal(42, movement.PurchaseOrderId);
        Assert.Equal(3, movement.SupplierId);
        Assert.Equal(TestData.Now, order.ReceivedAt);
        Assert.False(order.IsActive);
    }

    [Fact(DisplayName = "No se recibe una orden que no está aprobada")]
    public void Receive_pending_order_is_not_allowed()
    {
        var order = NewOrder(TestData.Supplier(), new PurchaseOrderLine(Flour, 25, 50_000));
        var flour = NewSupply(stock: 40);

        Assert.Throws<InvalidOrderStateException>(() =>
            order.Receive(new Dictionary<int, Supply> { [Flour] = flour }, TestData.UserId, TestData.Today, TestData.Now));
        Assert.Equal(40, flour.CurrentStock);
    }

    [Fact(DisplayName = "Para recibir se necesitan todos los insumos de la orden")]
    public void Receive_requires_every_supply()
    {
        var supplier = TestData.Supplier();
        var order = NewOrder(supplier);
        order.Approve(supplier, TestData.UserId, TestData.Now);

        Assert.Throws<InvalidPurchaseOrderException>(() =>
            order.Receive(new Dictionary<int, Supply> { [Flour] = NewSupply(0) }, TestData.UserId, TestData.Today, TestData.Now));
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
    }

    [Fact(DisplayName = "La orden sale de la cotización ganadora con sus precios")]
    public void FromQuotation_copies_winning_prices()
    {
        var supplier = TestData.Supplier(id: 5);
        var request = RequestForQuotation.Create([new RfqLine(Flour, 25), new RfqLine(Sugar, 10)],
            TestData.Today.AddDays(3), TestData.Today, TestData.UserId);
        request.Id = 9;
        var quotation = request.RegisterQuotation(supplier,
            new Dictionary<int, decimal> { [Flour] = 50_000, [Sugar] = 45_000 }, 2, TestData.Now);
        quotation.Id = 11;
        request.SelectQuotation(quotation, TestData.Now);

        var order = PurchaseOrder.FromQuotation(quotation, TestData.Today.AddDays(2), TestData.Today, TestData.UserId);

        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Equal(5, order.SupplierId);
        Assert.Equal(11, order.QuotationId);
        Assert.Equal(quotation.Total, order.Total);
        Assert.Equal(50_000, Assert.Single(order.Items, i => i.SupplyId == Flour).UnitPrice);
    }

    [Fact(DisplayName = "Solo la cotización ganadora genera orden")]
    public void FromQuotation_requires_selected_quotation()
    {
        var request = RequestForQuotation.Create([new RfqLine(Flour, 1)], TestData.Today, TestData.Today, TestData.UserId);
        var quotation = request.RegisterQuotation(TestData.Supplier(), new Dictionary<int, decimal> { [Flour] = 1_000 }, 1, TestData.Now);

        Assert.Throws<InvalidPurchaseOrderException>(() =>
            PurchaseOrder.FromQuotation(quotation, TestData.Today, TestData.Today, TestData.UserId));
    }

    private static Exception? Record(Action action)
    {
        try { action(); return null; }
        catch (Exception e) { return e; }
    }
}
