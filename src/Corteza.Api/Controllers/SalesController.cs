using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Módulo 02 · Pedidos (02.1 a 02.3) y cobro (02.5).</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.Sales)]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<OrderListItem>> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(query, ct);

    [HttpGet("summary")]
    public Task<OrderSummary> Summary(CancellationToken ct) => orders.SummaryAsync(ct);

    [HttpGet("{id:int}")]
    public Task<OrderDetail> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<OrderDetail>> Create(OrderCreateRequest request, CancellationToken ct)
    {
        var created = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Recibido → En preparación → Listo → Entregado, o Cancelado.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<OrderDetail> Advance(int id, AdvanceOrderRequest request, CancellationToken ct) => orders.AdvanceAsync(id, request, ct);

    [HttpPost("{id:int}/request-bill")]
    public Task<OrderDetail> RequestBill(int id, CancellationToken ct) => orders.RequestBillAsync(id, ct);

    /// <summary>Cobra: emite la factura de venta, calcula el cambio y libera la mesa.</summary>
    [HttpPost("{id:int}/checkout")]
    public Task<SalesInvoiceDetail> Checkout(int id, CheckoutRequest request, CancellationToken ct) => orders.CheckoutAsync(id, request, ct);
}

/// <summary>Pantalla 02.4 · Estado de las mesas.</summary>
[ApiController]
[Route("api/dining-tables")]
[Authorize(Policy = Policies.Sales)]
public sealed class DiningTablesController(OrderService orders) : ControllerBase
{
    [HttpGet("status")]
    public Task<IReadOnlyList<DiningTableStatus>> Status(CancellationToken ct) => orders.TablesAsync(ct);
}

/// <summary>Pantalla 02.6 · Facturas de venta.</summary>
[ApiController]
[Route("api/sales-invoices")]
[Authorize(Policy = Policies.Sales)]
public sealed class SalesInvoicesController(SalesInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SalesInvoiceListItem>> List([FromQuery] SalesInvoiceQuery query, CancellationToken ct) =>
        invoices.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<SalesInvoiceDetail> Get(int id, CancellationToken ct) => invoices.GetAsync(id, ct);

    [HttpPost("{id:int}/void")]
    public Task<SalesInvoiceDetail> Void(int id, VoidSalesInvoiceRequest request, CancellationToken ct) => invoices.VoidAsync(id, request, ct);
}
