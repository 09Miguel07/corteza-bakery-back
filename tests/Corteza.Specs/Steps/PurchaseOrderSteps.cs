using Corteza.Application.Features.Purchasing;
using Corteza.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class PurchaseOrderSteps(SpecsWorld world)
{
    [Given(@"^que existe una orden de (\d+) (\w+) de ""([^""]+)"" a (\d+) pesos para ""([^""]+)""$")]
    public async Task GivenOrder(decimal quantity, string unitLabel, string supply, decimal price, string supplier)
    {
        var order = await world.Orders.CreateAsync(new PurchaseOrderCreateRequest
        {
            SupplierId = world.SupplierIds[supplier],
            ExpectedDeliveryDate = world.Today.AddDays(2),
            Items = [new PurchaseOrderLineRequest { SupplyId = world.SupplyIds[supply], Quantity = quantity, UnitPrice = price }],
        }, default);
        world.OrderId = order.Id;
        world.Db.ChangeTracker.Clear();
    }

    [Given(@"^que la orden ya fue aprobada$")]
    public async Task GivenApproved()
    {
        await world.Orders.ApproveAsync(world.OrderId, default);
        world.Db.ChangeTracker.Clear();
    }

    [When(@"^el jefe de compras aprueba la orden$")]
    public Task WhenApprove() =>
        world.RunAsync(() => world.Orders.ApproveAsync(world.OrderId, default), "Orden aprobada");

    [When(@"^cancelo la orden por ""([^""]+)""$")]
    public Task WhenCancel(string reason) =>
        world.RunAsync(() => world.Orders.CancelAsync(world.OrderId, new CancelPurchaseOrderRequest { Reason = reason }, default),
            "Orden cancelada");

    [When(@"^registro la recepción de la orden$")]
    public Task WhenReceive() =>
        world.RunAsync(() => world.Orders.ReceiveAsync(world.OrderId, new ReceivePurchaseOrderRequest(), default),
            "Mercancía recibida");

    [Then(@"^la orden (?:cambia a|sigue en) ""([^""]+)""$")]
    public async Task ThenOrderState(string state)
    {
        await using var db = world.Fresh();
        var order = await db.PurchaseOrders.SingleAsync(o => o.Id == world.OrderId);
        Assert.Equal(state, Labels.Of(order.Status));
    }

    [Then(@"^el presupuesto de ""([^""]+)"" (?:se reduce a|sigue en) (\d+) pesos$")]
    public async Task ThenBudget(string supplier, decimal budget)
    {
        await using var db = world.Fresh();
        Assert.Equal(budget, (await db.Suppliers.SingleAsync(s => s.Id == world.SupplierIds[supplier])).AvailableBudget);
    }

    [Then(@"^el sistema responde que la orden ya está aprobada$")]
    public void ThenAlreadyApproved()
    {
        Assert.NotNull(world.LastError);
        Assert.Contains("ya está aprobada", world.LastError!.Message);
    }
}
