using Corteza.Api.Auth;
using Corteza.Application.Features.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Pantalla 07.1 · ¿Cómo va la panadería? Solo el administrador.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.Admin)]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("overview")]
    public Task<ReportOverview> Overview([FromQuery] ReportRange range = ReportRange.Week, CancellationToken ct = default) =>
        reports.OverviewAsync(range, ct);
}
