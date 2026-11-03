using Corteza.Application.Features.Quotations;
using Corteza.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class QuotationSteps(SpecsWorld world)
{
    [Given(@"^que existe una solicitud abierta por (\d+) (\w+) de ""([^""]+)""$")]
    public async Task GivenOpenRequest(decimal quantity, string unitLabel, string supply)
    {
        var request = await world.Quotations.CreateAsync(new RfqCreateRequest
        {
            ResponseDeadline = world.Today.AddDays(3),
            Items = [new RfqLineRequest { SupplyId = world.SupplyIds[supply], Quantity = quantity }],
        }, default);
        world.RequestId = request.Id;
    }

    [Given(@"^que ""([^""]+)"" ya cotizó la solicitud a (\d+) pesos por (\w+)$")]
    public async Task GivenSupplierAlreadyQuoted(string supplier, decimal price, string unit)
    {
        await Quote(supplier, price);
        Assert.Null(world.LastError);
    }

    [When(@"^""([^""]+)"" cotiza la solicitud a (-?\d+) pesos por (\w+)$")]
    public Task WhenSupplierQuotes(string supplier, decimal price, string unit) => Quote(supplier, price);

    [When(@"^elijo la cotización de ""([^""]+)""$")]
    public async Task WhenSelect(string supplier)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        var quotation = detail.Quotations.Single(q => q.SupplierName == supplier);
        await world.RunAsync(async () =>
        {
            var result = await world.Quotations.SelectAsync(world.RequestId, quotation.Id, new SelectQuotationRequest(), default);
            world.CreatedOrderId = result.PurchaseOrderId;
        }, "Cotización elegida");
    }

    [Then(@"^la cotización de ""([^""]+)"" queda en estado ""([^""]+)""$")]
    public async Task ThenQuotationState(string supplier, string state)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        Assert.Equal(state, Labels.Of(detail.Quotations.Single(q => q.SupplierName == supplier).Status));
    }

    [Then(@"^su total es (\d+) pesos$")]
    public async Task ThenTotal(decimal total)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        Assert.Equal(total, detail.Quotations.Single(q => q.SupplierName == world.LastQuotationSupplier).Total);
    }

    [Then(@"^la solicitud tiene (\d+) cotizaci(?:ón|ones)$")]
    public async Task ThenQuotationCount(int count)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        Assert.Equal(count, detail.Quotations.Count);
    }

    [Then(@"^la solicitud queda en estado ""([^""]+)""$")]
    public async Task ThenRequestState(string state)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        Assert.Equal(state, Labels.Of(detail.Status));
    }

    [Then(@"^se crea una orden de compra ""([^""]+)"" para ""([^""]+)"" por (\d+) pesos$")]
    public async Task ThenOrderCreated(string state, string supplier, decimal total)
    {
        Assert.NotNull(world.CreatedOrderId);
        await using var db = world.Fresh();
        var order = await db.PurchaseOrders.Include(o => o.Supplier).SingleAsync(o => o.Id == world.CreatedOrderId);
        Assert.Equal(state, Labels.Of(order.Status));
        Assert.Equal(supplier, order.Supplier.Name);
        Assert.Equal(total, order.Total);
    }

    private async Task Quote(string supplier, decimal price)
    {
        var detail = await world.Quotations.GetAsync(world.RequestId, default);
        world.LastQuotationSupplier = supplier;
        await world.RunAsync(() => world.Quotations.RegisterQuotationAsync(world.RequestId, new QuotationCreateRequest
        {
            SupplierId = world.SupplierIds[supplier],
            DeliveryDays = 2,
            Prices = detail.Items.Select(i => new QuotationPriceRequest { SupplyId = i.SupplyId, UnitPrice = price }).ToList(),
        }, default), "Cotización registrada");
    }
}
