using Corteza.Api.Auth;
using Corteza.Application.Features.Payroll;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Pantalla 06.0 · Calendario de pagos: periodos de nómina.</summary>
[ApiController]
[Route("api/payroll-periods")]
[Authorize(Policy = Policies.Hr)]
public sealed class PayrollPeriodsController(PayrollService payroll) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PayrollPeriodListItem>> List([FromQuery] int? year, CancellationToken ct) => payroll.ListAsync(year, ct);

    [HttpGet("{id:int}")]
    public Task<PayrollPeriodDetail> Get(int id, CancellationToken ct) => payroll.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PayrollPeriodDetail>> Create(PayrollPeriodCreateRequest request, CancellationToken ct)
    {
        var created = await payroll.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Calcula los comprobantes de los empleados activos (solo en borrador).</summary>
    [HttpPost("{id:int}/generate")]
    public Task<PayrollPeriodDetail> Generate(int id, CancellationToken ct) => payroll.GenerateAsync(id, ct);

    [HttpPost("{id:int}/approve")]
    public Task<PayrollPeriodDetail> Approve(int id, CancellationToken ct) => payroll.ApproveAsync(id, ct);

    [HttpPost("{id:int}/pay")]
    public Task<PayrollPeriodDetail> Pay(int id, CancellationToken ct) => payroll.PayAsync(id, ct);
}

/// <summary>Pantalla 06.3 · Comprobante de nómina.</summary>
[ApiController]
[Route("api/payslips")]
[Authorize(Policy = Policies.Hr)]
public sealed class PayslipsController(PayrollService payroll) : ControllerBase
{
    [HttpGet("{id:int}")]
    public Task<PayslipDetail> Get(int id, CancellationToken ct) => payroll.GetPayslipAsync(id, ct);
}
