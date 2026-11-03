using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Módulo 06 · Talento Humano: empleados.</summary>
[ApiController]
[Route("api/employees")]
[Authorize(Policy = Policies.Hr)]
public sealed class EmployeesController(EmployeeService employees) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<EmployeeListItem>> List([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        employees.ListAsync(query, ct);

    [HttpGet("summary")]
    public Task<EmployeeSummary> Summary(CancellationToken ct) => employees.SummaryAsync(ct);

    [HttpGet("positions")]
    public Task<IReadOnlyList<string>> Positions(CancellationToken ct) => employees.PositionsAsync(ct);

    [HttpGet("{id:int}")]
    public Task<EmployeeDetail> Get(int id, CancellationToken ct) => employees.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<EmployeeDetail>> Create(EmployeeCreateRequest request, CancellationToken ct)
    {
        var created = await employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<EmployeeDetail> Update(int id, EmployeeUpdateRequest request, CancellationToken ct) =>
        employees.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await employees.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        await employees.ActivateAsync(id, ct);
        return NoContent();
    }
}
