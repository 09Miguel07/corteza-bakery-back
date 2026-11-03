using Corteza.Api.Auth;
using Corteza.Application.Common.Models;
using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Quotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

/// <summary>Módulo 05 · Compras: órdenes de compra (pantallas 05.1 a 05.3).</summary>
[ApiController]
[Route("api/purchase-orders")]
[Authorize(Policy = Policies.Purchases)]
public sealed class PurchaseOrdersController(PurchaseOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseOrderListItem>> List([FromQuery] PurchaseOrderQuery query, CancellationToken ct) =>
        orders.ListAsync(query, ct);

    [HttpGet("summary")]
    public Task<PurchaseOrderSummary> Summary(CancellationToken ct) => orders.SummaryAsync(ct);

    [HttpGet("{id:int}")]
    public Task<PurchaseOrderDetail> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetail>> Create(PurchaseOrderCreateRequest request, CancellationToken ct)
    {
        var created = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Aprueba y descuenta el presupuesto del proveedor (422 si no alcanza o ya está aprobada).</summary>
    [HttpPost("{id:int}/approve")]
    public Task<PurchaseOrderDetail> Approve(int id, CancellationToken ct) => orders.ApproveAsync(id, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<PurchaseOrderDetail> Cancel(int id, CancelPurchaseOrderRequest request, CancellationToken ct) =>
        orders.CancelAsync(id, request, ct);

    /// <summary>"Recibir mercancía": suma cada línea al inventario.</summary>
    [HttpPost("{id:int}/receive")]
    public Task<PurchaseOrderDetail> Receive(int id, ReceivePurchaseOrderRequest request, CancellationToken ct) =>
        orders.ReceiveAsync(id, request, ct);
}

/// <summary>Solicitudes de cotización y ofertas de proveedores (taller 2).</summary>
[ApiController]
[Route("api/quotation-requests")]
[Authorize(Policy = Policies.Purchases)]
public sealed class QuotationRequestsController(QuotationService quotations) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<RfqListItem>> List([FromQuery] RfqQuery query, CancellationToken ct) =>
        quotations.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<RfqDetail> Get(int id, CancellationToken ct) => quotations.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<RfqDetail>> Create(RfqCreateRequest request, CancellationToken ct)
    {
        var created = await quotations.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/quotations")]
    public Task<RfqDetail> RegisterQuotation(int id, QuotationCreateRequest request, CancellationToken ct) =>
        quotations.RegisterQuotationAsync(id, request, ct);

    /// <summary>Elige la ganadora: cierra la solicitud y crea la orden de compra pendiente.</summary>
    [HttpPost("{id:int}/quotations/{quotationId:int}/select")]
    public Task<SelectQuotationResult> Select(int id, int quotationId, SelectQuotationRequest request, CancellationToken ct) =>
        quotations.SelectAsync(id, quotationId, request, ct);

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await quotations.CancelAsync(id, ct);
        return NoContent();
    }
}
