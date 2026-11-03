using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Inventory;
using Corteza.Application.Features.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Categorías de insumo: las usan Inventarios y Proveedores.</summary>
[ApiController]
[Route("api/supply-categories")]
[Authorize]
public sealed class SupplyCategoriesController(SupplyCategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryRef>> List(CancellationToken ct) => categories.ListAsync(ct);
}

/// <summary>Módulo 03 · Inventarios: insumos.</summary>
[ApiController]
[Route("api/supplies")]
[Authorize(Policy = Policies.InventoryRead)]
public sealed class SuppliesController(SupplyService supplies, InventoryMovementService movements) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupplyListItem>> List([FromQuery] SupplyQuery query, CancellationToken ct) =>
        supplies.ListAsync(query, ct);

    [HttpGet("low-stock")]
    public Task<IReadOnlyList<SupplyListItem>> LowStock(CancellationToken ct) => supplies.LowStockAsync(ct);

    [HttpGet("{id:int}")]
    public Task<SupplyListItem> Get(int id, CancellationToken ct) => supplies.GetAsync(id, ct);

    [HttpGet("{id:int}/movements")]
    public Task<PagedResult<MovementDto>> Movements(int id, [FromQuery] MovementQuery query, CancellationToken ct) =>
        movements.ListBySupplyAsync(id, query, ct);

    [HttpPost]
    [Authorize(Policy = Policies.InventoryWrite)]
    public async Task<ActionResult<SupplyListItem>> Create(SupplyCreateRequest request, CancellationToken ct)
    {
        var created = await supplies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.InventoryWrite)]
    public Task<SupplyListItem> Update(int id, SupplyUpdateRequest request, CancellationToken ct) =>
        supplies.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Policy = Policies.InventoryWrite)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await supplies.SetActiveAsync(id, false, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    [Authorize(Policy = Policies.InventoryWrite)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        await supplies.SetActiveAsync(id, true, ct);
        return NoContent();
    }
}

/// <summary>Entradas y salidas de bodega (pantalla 03.3).</summary>
[ApiController]
[Route("api/inventory-movements")]
[Authorize(Policy = Policies.InventoryWrite)]
public sealed class InventoryMovementsController(InventoryMovementService movements) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MovementDto>> Register(MovementCreateRequest request, CancellationToken ct)
    {
        var movement = await movements.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, movement);
    }
}
