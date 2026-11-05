using Corteza.Application.Features.Sales;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Specs.Support;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class SalesSteps(SpecsWorld world)
{
    [Given(@"^que el producto ""([^""]+)"" vale (\d+) pesos$")]
    public async Task GivenProduct(string name, decimal price)
    {
        var product = world.Db.Products.Add(new Product { Name = name, Price = price, Category = ProductCategory.Bakery }).Entity;
        await world.Db.SaveChangesAsync();
        world.ProductIds[name] = product.Id;
    }

    [Given(@"^que la mesa (\d+) está libre$")]
    public async Task GivenFreeTable(short number)
    {
        var table = world.Db.DiningTables.Add(new DiningTable { Number = number, Capacity = 4 }).Entity;
        await world.Db.SaveChangesAsync();
        world.TableIds[number] = table.Id;
    }

    [Given(@"^que la mesa (\d+) pidió (\d+) ""([^""]+)"" y (\d+) ""([^""]+)""$")]
    public async Task GivenOrder(int table, short q1, string p1, short q2, string p2)
    {
        var order = await world.Orders2.CreateAsync(new OrderCreateRequest
        {
            ServiceType = ServiceType.DineIn,
            DiningTableId = world.TableIds[table],
            Items = [new OrderItemRequest { ProductId = world.ProductIds[p1], Quantity = q1 }, new OrderItemRequest { ProductId = world.ProductIds[p2], Quantity = q2 }],
        }, default);
        world.SalesOrderId = order.Id;
        world.Db.ChangeTracker.Clear();
    }

    [When(@"^cobro el pedido en efectivo con (\d+) pesos$")]
    public Task WhenCheckoutCash(decimal received) =>
        world.RunAsync(async () => world.LastInvoice = await world.Orders2.CheckoutAsync(world.SalesOrderId,
            new CheckoutRequest { PaymentMethod = PaymentMethod.Cash, AmountReceived = received }, default), "Factura emitida");

    [When(@"^intento abrir otro pedido en la mesa (\d+)$")]
    public Task WhenAnotherOrder(int table) =>
        world.RunAsync(() => world.Orders2.CreateAsync(new OrderCreateRequest
        {
            ServiceType = ServiceType.DineIn,
            DiningTableId = world.TableIds[table],
            Items = [new OrderItemRequest { ProductId = world.ProductIds.Values.First(), Quantity = 1 }],
        }, default), "Pedido creado");

    [Then(@"^el cambio es (\d+) pesos$")]
    public void ThenChange(decimal change) => Assert.Equal(change, world.LastInvoice!.ChangeGiven);

    [Then(@"^la mesa (\d+) queda ""(LIBRE|OCUPADA|POR COBRAR)""$")]
    public async Task ThenTableState(int table, string state)
    {
        var tables = await world.Orders2.TablesAsync(default);
        var expected = state switch { "LIBRE" => TableState.Free, "OCUPADA" => TableState.Occupied, _ => TableState.AwaitingPayment };
        Assert.Equal(expected, tables.Single(t => t.Id == world.TableIds[table]).State);
    }
}
