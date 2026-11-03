using Corteza.Specs.Support;
using Reqnroll;

namespace Corteza.Specs.Steps;

[Binding]
public sealed class CommonSteps(SpecsWorld world)
{
    [Given(@"^que el insumo ""([^""]+)"" tiene (-?\d+) (\w+) en bodega$")]
    public Task GivenSupplyWithStock(string supply, decimal stock, string unitLabel) =>
        world.AddSupplyAsync(supply, stock, unitLabel);

    [Given(@"^que el proveedor ""([^""]+)"" con NIT ""([^""]+)"" está activo$")]
    public Task GivenActiveSupplier(string supplier, string taxId) =>
        world.AddSupplierAsync(supplier, taxId);

    [Given(@"^que el proveedor ""([^""]+)"" con NIT ""([^""]+)"" tiene presupuesto disponible de (\d+) pesos$")]
    public Task GivenSupplierWithBudget(string supplier, string taxId, decimal budget) =>
        world.AddSupplierAsync(supplier, taxId, budget);

    /// <summary>Acepta el mensaje completo o su inicio ("Stock insuficiente", "Monto inválido").</summary>
    [Then(@"^el sistema responde ""(.*)""$")]
    public void ThenSystemResponds(string expected)
    {
        Assert.NotNull(world.LastResponse);
        Assert.True(world.LastResponse == expected || world.LastResponse!.StartsWith(expected),
            $"Esperaba \"{expected}\" y el sistema respondió \"{world.LastResponse}\".");
    }
}
