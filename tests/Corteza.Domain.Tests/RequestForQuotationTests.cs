using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>
/// Taller 2 · Solicitud de cotizaciones: solo se cotiza sobre solicitudes abiertas, el monto debe ser mayor
/// que cero y un proveedor no puede cotizar dos veces la misma solicitud.
/// </summary>
public class RequestForQuotationTests
{
    private const int Flour = 1;
    private const int Sugar = 2;

    private static RequestForQuotation NewRequest(params RfqLine[] lines) =>
        RequestForQuotation.Create(
            lines.Length > 0 ? lines : [new RfqLine(Flour, 25), new RfqLine(Sugar, 10)],
            responseDeadline: TestData.Today.AddDays(3), TestData.Today, TestData.UserId, "Compra de la semana");

    private static Dictionary<int, decimal> Prices(decimal flour = 50_000, decimal sugar = 45_000) =>
        new() { [Flour] = flour, [Sugar] = sugar };

    [Fact(DisplayName = "Una solicitud nueva queda abierta con sus insumos")]
    public void Create_starts_open()
    {
        var request = NewRequest();

        Assert.Equal(RfqStatus.Open, request.Status);
        Assert.Equal(2, request.Items.Count);
        Assert.Empty(request.Quotations);
    }

    [Fact(DisplayName = "Una solicitud necesita al menos un insumo")]
    public void Create_requires_items()
    {
        Assert.Throws<InvalidRequestForQuotationException>(() =>
            RequestForQuotation.Create([], TestData.Today, TestData.Today, TestData.UserId));
    }

    [Theory(DisplayName = "La cantidad pedida debe ser mayor que cero")]
    [InlineData(0)]
    [InlineData(-4)]
    public void Create_rejects_non_positive_quantities(decimal quantity)
    {
        Assert.Throws<InvalidQuantityException>(() => NewRequest(new RfqLine(Flour, quantity)));
    }

    [Fact(DisplayName = "Un insumo no se repite en la misma solicitud")]
    public void Create_rejects_duplicated_supplies()
    {
        Assert.Throws<InvalidRequestForQuotationException>(() => NewRequest(new RfqLine(Flour, 5), new RfqLine(Flour, 3)));
    }

    [Fact(DisplayName = "La fecha límite no puede ser anterior a hoy")]
    public void Create_rejects_past_deadline()
    {
        Assert.Throws<InvalidRequestForQuotationException>(() =>
            RequestForQuotation.Create([new RfqLine(Flour, 5)], TestData.Today.AddDays(-1), TestData.Today, TestData.UserId));
    }

    [Fact(DisplayName = "Registrar una cotización válida la deja RECIBIDA con su total")]
    public void RegisterQuotation_valid_is_received_with_total()
    {
        var request = NewRequest();
        var supplier = TestData.Supplier(id: 1);

        var quotation = request.RegisterQuotation(supplier, Prices(), deliveryDays: 2, TestData.Now);

        Assert.Equal(QuotationStatus.Received, quotation.Status);
        Assert.Equal(25 * 50_000 + 10 * 45_000, quotation.Total); // 1.700.000
        Assert.Equal(2, quotation.Items.Count);
        Assert.Same(quotation, Assert.Single(request.Quotations));
    }

    [Theory(DisplayName = "Validar monto de cotización")]
    [InlineData(2_500_000, true)]
    [InlineData(0, false)]
    [InlineData(-1_000, false)]
    public void RegisterQuotation_validates_amount(decimal amount, bool accepted)
    {
        var request = NewRequest(new RfqLine(Flour, 1));
        var supplier = TestData.Supplier(id: 1);
        var prices = new Dictionary<int, decimal> { [Flour] = amount };

        if (accepted)
        {
            var quotation = request.RegisterQuotation(supplier, prices, 1, TestData.Now);
            Assert.Equal(amount, quotation.Total);
        }
        else
        {
            var error = Assert.Throws<InvalidAmountException>(() => request.RegisterQuotation(supplier, prices, 1, TestData.Now));
            Assert.StartsWith("Monto inválido", error.Message);
            Assert.Empty(request.Quotations);
        }
    }

    [Fact(DisplayName = "Falta el precio de un insumo: monto inválido")]
    public void RegisterQuotation_requires_a_price_for_every_item()
    {
        var request = NewRequest();
        var onlyFlour = new Dictionary<int, decimal> { [Flour] = 50_000 };

        Assert.Throws<InvalidAmountException>(() => request.RegisterQuotation(TestData.Supplier(), onlyFlour, 1, TestData.Now));
    }

    [Fact(DisplayName = "Un proveedor no cotiza dos veces la misma solicitud")]
    public void RegisterQuotation_rejects_duplicate_supplier()
    {
        var request = NewRequest();
        var supplier = TestData.Supplier(id: 1);
        request.RegisterQuotation(supplier, Prices(), 2, TestData.Now);

        Assert.Throws<DuplicateQuotationException>(() => request.RegisterQuotation(supplier, Prices(40_000), 1, TestData.Now));
        Assert.Single(request.Quotations);
    }

    [Fact(DisplayName = "Un proveedor inactivo no puede cotizar")]
    public void RegisterQuotation_rejects_inactive_supplier()
    {
        var request = NewRequest();

        Assert.Throws<InactiveSupplierException>(() =>
            request.RegisterQuotation(TestData.Supplier(active: false), Prices(), 2, TestData.Now));
    }

    [Fact(DisplayName = "No se cotiza una solicitud cerrada")]
    public void RegisterQuotation_on_closed_request_is_not_allowed()
    {
        var request = NewRequest();
        var first = request.RegisterQuotation(TestData.Supplier(id: 1), Prices(), 2, TestData.Now);
        request.SelectQuotation(first, TestData.Now);

        Assert.Throws<QuotationNotAllowedException>(() =>
            request.RegisterQuotation(TestData.Supplier(id: 2), Prices(), 2, TestData.Now));
    }

    [Fact(DisplayName = "Elegir la cotización ganadora rechaza las demás y cierra la solicitud")]
    public void SelectQuotation_closes_request_and_rejects_the_rest()
    {
        var request = NewRequest();
        var expensive = request.RegisterQuotation(TestData.Supplier(id: 1), Prices(55_000, 46_000), 2, TestData.Now);
        var cheap = request.RegisterQuotation(TestData.Supplier(id: 2), Prices(50_000, 45_000), 3, TestData.Now);

        request.SelectQuotation(cheap, TestData.Now);

        Assert.Equal(QuotationStatus.Selected, cheap.Status);
        Assert.Equal(QuotationStatus.Rejected, expensive.Status);
        Assert.Equal(RfqStatus.Closed, request.Status);
        Assert.Equal(TestData.Now, request.ClosedAt);
    }

    [Fact(DisplayName = "La mejor oferta es la de menor total")]
    public void LowestQuotation_returns_cheapest()
    {
        var request = NewRequest();
        request.RegisterQuotation(TestData.Supplier(id: 1), Prices(55_000, 46_000), 2, TestData.Now);
        var cheap = request.RegisterQuotation(TestData.Supplier(id: 2), Prices(50_000, 45_000), 3, TestData.Now);

        Assert.Same(cheap, request.LowestQuotation);
    }

    [Fact(DisplayName = "No se elige una cotización de otra solicitud")]
    public void SelectQuotation_rejects_foreign_quotation()
    {
        var request = NewRequest();
        var other = NewRequest();
        var foreign = other.RegisterQuotation(TestData.Supplier(id: 1), Prices(), 2, TestData.Now);

        Assert.Throws<InvalidRequestForQuotationException>(() => request.SelectQuotation(foreign, TestData.Now));
        Assert.Equal(RfqStatus.Open, request.Status);
    }

    [Fact(DisplayName = "Cancelar una solicitud abierta la deja cancelada")]
    public void Cancel_open_request()
    {
        var request = NewRequest();

        request.Cancel(TestData.Now);

        Assert.Equal(RfqStatus.Cancelled, request.Status);
    }

    [Fact(DisplayName = "No se cancela una solicitud ya cerrada")]
    public void Cancel_closed_request_is_not_allowed()
    {
        var request = NewRequest();
        request.SelectQuotation(request.RegisterQuotation(TestData.Supplier(), Prices(), 1, TestData.Now), TestData.Now);

        Assert.Throws<QuotationNotAllowedException>(() => request.Cancel(TestData.Now));
    }
}
