using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Módulo 04 · Proveedores.</summary>
[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = Policies.SuppliersRead)]
public sealed class SuppliersController(SupplierService suppliers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupplierListItem>> List([FromQuery] SupplierQuery query, CancellationToken ct) =>
        suppliers.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<SupplierDetail> Get(int id, CancellationToken ct) => suppliers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.SuppliersWrite)]
    public async Task<ActionResult<SupplierDetail>> Create(SupplierUpsertRequest request, CancellationToken ct)
    {
        var created = await suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.SuppliersWrite)]
    public Task<SupplierDetail> Update(int id, SupplierUpsertRequest request, CancellationToken ct) =>
        suppliers.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Policy = Policies.SuppliersWrite)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await suppliers.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    [Authorize(Policy = Policies.SuppliersWrite)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        await suppliers.ActivateAsync(id, ct);
        return NoContent();
    }
}
