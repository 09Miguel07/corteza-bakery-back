using Corteza.Application.Features.Inventory;
using Corteza.Domain.Enums;
using Corteza.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class InventorySteps(SpecsWorld world)
{
    [When(@"^se retiran (\d+) (\w+) de ""([^""]+)""$")]
    public Task WhenWithdraw(decimal quantity, string unitLabel, string supply) =>
        world.RunAsync(() => world.Movements.RegisterAsync(new MovementCreateRequest
        {
            SupplyId = world.SupplyIds[supply],
            Type = MovementType.Out,
            Quantity = quantity,
        }, default), "Retiro exitoso");

    [When(@"^registro una entrada de (\d+) (\w+) de ""([^""]+)""$")]
    public Task WhenEntry(decimal quantity, string unitLabel, string supply) =>
        world.RunAsync(() => world.Movements.RegisterAsync(new MovementCreateRequest
        {
            SupplyId = world.SupplyIds[supply],
            Type = MovementType.In,
            Quantity = quantity,
        }, default), "Entrada registrada");

    [When(@"^registro el insumo ""([^""]+)"" con stock inicial (-?\d+)$")]
    public async Task WhenRegisterSupply(string name, decimal initialStock)
    {
        var categoryId = await world.CategoryIdAsync();
        await world.RunAsync(() => world.Supplies.CreateAsync(new SupplyCreateRequest
        {
            Name = name,
            CategoryId = categoryId,
            PurchaseUnit = "Caja de 25 kg",
            UnitLabel = "cajas",
            InitialStock = initialStock,
            MinStock = 5,
        }, default), "Insumo registrado");
    }

    [Then(@"^el stock final de ""([^""]+)"" es (\d+)$")]
    public async Task ThenFinalStock(string supply, decimal expected)
    {
        await using var db = world.Fresh();
        Assert.Equal(expected, (await db.Supplies.SingleAsync(s => s.Id == world.SupplyIds[supply])).CurrentStock);
    }
}
