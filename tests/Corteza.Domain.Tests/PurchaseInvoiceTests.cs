using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>Pantalla 05.4 · Facturas de compra: lo que nos cobran los proveedores, por pagar y vencidas.</summary>
public class PurchaseInvoiceTests
{
    private static PurchaseOrder OrderFor(Supplier supplier) =>
        PurchaseOrder.Create(supplier, [new PurchaseOrderLine(1, 10, 50_000)], TestData.Today.AddDays(2), TestData.Today, TestData.UserId);

    private static PurchaseInvoice Register(Supplier? supplier = null, decimal total = 500_000, DateOnly? issue = null,
        DateOnly? due = null, PurchaseOrder? order = null, string number = "FC-9921") =>
        PurchaseInvoice.Create(supplier ?? TestData.Supplier(), number, issue ?? TestData.Today, due ?? TestData.Today.AddDays(30),
            total, order, "Harina de noviembre");

    [Fact(DisplayName = "Una factura nueva queda pendiente de pago")]
    public void Create_starts_pending()
    {
        var invoice = Register();

        Assert.Equal(PurchaseInvoiceStatus.Pending, invoice.Status);
        Assert.Equal(500_000, invoice.Total);
        Assert.Equal("FC-9921", invoice.SupplierInvoiceNumber);
        Assert.Null(invoice.PaidAt);
    }

    [Theory(DisplayName = "El total de la factura debe ser mayor que cero")]
    [InlineData(0)]
    [InlineData(-1000)]
    public void Create_rejects_non_positive_total(decimal total)
    {
        var error = Assert.Throws<InvalidAmountException>(() => Register(total: total));
        Assert.Equal("Monto inválido: debe ser mayor que cero.", error.Message);
    }

    [Fact(DisplayName = "El vencimiento no puede ser anterior a la emisión")]
    public void Create_rejects_due_before_issue()
    {
        var error = Assert.Throws<InvalidPurchaseInvoiceException>(() => Register(due: TestData.Today.AddDays(-1)));
        Assert.Contains("vencimiento", error.Message);
    }

    [Fact(DisplayName = "El número de la factura del proveedor es obligatorio")]
    public void Create_requires_number()
    {
        Assert.Throws<InvalidPurchaseInvoiceException>(() => Register(number: "  "));
    }

    [Fact(DisplayName = "La orden asociada debe ser del mismo proveedor")]
    public void Create_rejects_order_of_other_supplier()
    {
        var order = OrderFor(TestData.Supplier(id: 2, taxId: "800111222-3"));

        var error = Assert.Throws<InvalidPurchaseInvoiceException>(() => Register(TestData.Supplier(id: 1), order: order));
        Assert.Contains("otro proveedor", error.Message);
    }

    [Fact(DisplayName = "No se factura una orden cancelada")]
    public void Create_rejects_cancelled_order()
    {
        var supplier = TestData.Supplier();
        var order = OrderFor(supplier);
        order.Cancel("Proveedor sin stock", TestData.Now);

        Assert.Throws<InvalidPurchaseInvoiceException>(() => Register(supplier, order: order));
    }

    [Fact(DisplayName = "Pagar marca la factura como pagada con la fecha")]
    public void Pay_marks_paid()
    {
        var invoice = Register();

        invoice.Pay(TestData.Now);

        Assert.Equal(PurchaseInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(TestData.Now, invoice.PaidAt);
    }

    [Fact(DisplayName = "Una factura pagada no se vuelve a pagar")]
    public void Pay_twice_fails()
    {
        var invoice = Register();
        invoice.Pay(TestData.Now);

        var error = Assert.Throws<InvalidPurchaseInvoiceException>(() => invoice.Pay(TestData.Now));
        Assert.Equal("La factura ya está pagada.", error.Message);
    }

    [Fact(DisplayName = "Vencida: pendiente y con el vencimiento ya pasado")]
    public void IsOverdue_only_when_pending_and_past_due()
    {
        var invoice = Register(issue: TestData.Today.AddDays(-40), due: TestData.Today.AddDays(-1));

        Assert.True(invoice.IsOverdue(TestData.Today));
        Assert.False(Register(due: TestData.Today).IsOverdue(TestData.Today)); // vence hoy: todavía no

        invoice.Pay(TestData.Now);
        Assert.False(invoice.IsOverdue(TestData.Today));
    }
}
