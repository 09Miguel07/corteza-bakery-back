using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Suppliers;
using Corteza.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class SupplierSteps(SpecsWorld world)
{
    private async Task<SupplierUpsertRequest> RequestAsync(string name, string taxId, string email) => new()
    {
        Name = name,
        TaxId = taxId,
        ContactName = "Ana Ruiz",
        Phone = "3001112233",
        Email = email,
        AvailableBudget = 5_000_000,
        CategoryIds = [await world.CategoryIdAsync()],
    };

    [Given(@"^que no existe un proveedor con NIT ""([^""]+)""$")]
    public async Task GivenNoSupplierWithNit(string taxId) =>
        Assert.False(await world.Db.Suppliers.AnyAsync(s => s.TaxId == taxId));

    [Given(@"^que ya existe un proveedor con NIT ""([^""]+)""$")]
    public Task GivenExistingSupplier(string taxId) => world.AddSupplierAsync("Proveedor existente", taxId);

    [Given(@"^que existe un proveedor ""([^""]+)"" (sin|con) órdenes de compra activas$")]
    public async Task GivenSupplierWithOrWithoutOrders(string name, string withOrders)
    {
        var supplierId = await world.AddSupplierAsync(name, "900123456");
        if (withOrders != "con") return;

        var supplyId = await world.AddSupplyAsync("Harina de trigo", 0, "bultos");
        await world.Orders.CreateAsync(new PurchaseOrderCreateRequest
        {
            SupplierId = supplierId,
            ExpectedDeliveryDate = world.Today.AddDays(1),
            Items = [new PurchaseOrderLineRequest { SupplyId = supplyId, Quantity = 10, UnitPrice = 50_000 }],
        }, default);
    }

    [When(@"^registro el proveedor con NIT ""([^""]+)"", nombre ""([^""]+)"" y correo ""([^""]*)""$")]
    public async Task WhenRegisterSupplier(string taxId, string name, string email)
    {
        var request = await RequestAsync(name, taxId, email);
        await world.RunAsync(async () =>
        {
            var created = await world.Suppliers.CreateAsync(request, default);
            world.SupplierIds[name] = created.Id;
        }, "Proveedor registrado");
    }

    [When(@"^intento registrar otro proveedor ""([^""]+)"" con el NIT ""([^""]+)""$")]
    public Task WhenRegisterAnotherSupplier(string name, string taxId) =>
        WhenRegisterSupplier(taxId, name, "otro@proveedor.com");

    [When(@"^solicito desactivar el proveedor ""([^""]+)""$")]
    public Task WhenDeactivate(string name) =>
        world.RunAsync(() => world.Suppliers.DeactivateAsync(world.SupplierIds[name], default), "Proveedor desactivado");

    [Then(@"^el proveedor ""([^""]+)"" queda en estado ""(ACTIVO|INACTIVO)""$")]
    public async Task ThenSupplierState(string name, string state)
    {
        await using var db = world.Fresh();
        var supplier = await db.Suppliers.SingleAsync(s => s.Id == world.SupplierIds[name]);
        Assert.Equal(state, supplier.IsActive ? "ACTIVO" : "INACTIVO");
    }

    [Then(@"^solo hay (\d+) proveedor(?:es)? con NIT ""([^""]+)""$")]
    public async Task ThenSuppliersWithNit(int count, string taxId)
    {
        await using var db = world.Fresh();
        Assert.Equal(count, await db.Suppliers.CountAsync(s => s.TaxId == taxId));
    }
}
