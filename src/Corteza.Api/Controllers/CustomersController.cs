using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Módulo 01 · CRM (Clientes).</summary>
[ApiController]
[Route("api/customers")]
[Authorize] // ojo: las políticas de clase y de método se suman (AND); por eso van por acción
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Sales)] // ventas busca clientes al tomar un pedido
    public Task<PagedResult<CustomerListItem>> List([FromQuery] CustomerQuery query, CancellationToken ct) =>
        customers.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Crm)]
    public Task<CustomerDetail> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.Crm)]
    public async Task<ActionResult<CustomerDetail>> Create(CustomerCreateRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Crm)]
    public Task<CustomerDetail> Update(int id, CustomerUpdateRequest request, CancellationToken ct) =>
        customers.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Policy = Policies.Crm)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await customers.SetActiveAsync(id, false, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    [Authorize(Policy = Policies.Crm)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        await customers.SetActiveAsync(id, true, ct);
        return NoContent();
    }
}
