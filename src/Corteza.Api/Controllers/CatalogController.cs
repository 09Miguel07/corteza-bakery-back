using Corteza.Api.Auth;
using Corteza.Application.Features.Catalog;
using Corteza.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Productos de venta (los usa Facturación y Ventas al tomar pedidos).</summary>
[ApiController]
[Route("api/products")]
[Authorize(Policy = Policies.Sales)]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> List([FromQuery] bool? active, CancellationToken ct) =>
        products.ListAsync(active ?? true, ct);
}

/// <summary>Contadores del inicio del administrador (pantalla 07.0).</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.Admin)]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummary> Summary(CancellationToken ct) => dashboard.GetSummaryAsync(ct);
}
