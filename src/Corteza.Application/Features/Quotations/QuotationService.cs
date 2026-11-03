using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Quotations;

/// <summary>
/// Solicitudes de cotización (taller 2): se piden precios a varios proveedores, se comparan
/// y la ganadora se convierte en orden de compra pendiente.
/// </summary>
public sealed class QuotationService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<PagedResult<RfqListItem>> ListAsync(RfqQuery q, CancellationToken ct)
    {
        var query = db.RequestsForQuotation.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Code))
        {
            var code = q.Code.Trim().ToUpperInvariant();
            query = query.Where(r => r.Code.Contains(code));
        }

        if (q.Status is { } status)
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(r => new RfqListItem(
                r.Id, r.Code, r.Description, r.Status, r.ResponseDeadline,
                r.Items.Count, r.Quotations.Count,
                r.Quotations.Min(x => (decimal?)x.Total),
                r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<RfqListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<RfqDetail> GetAsync(int id, CancellationToken ct)
    {
        var request = await db.RequestsForQuotation.AsNoTracking()
            .Include(r => r.CreatedBy).ThenInclude(u => u.Employee)
            .Include(r => r.Items).ThenInclude(i => i.Supply)
            .Include(r => r.Quotations).ThenInclude(q => q.Supplier)
            .Include(r => r.Quotations).ThenInclude(q => q.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFoundException.For("la solicitud de cotización", id);

        var order = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Quotation != null && o.Quotation.RequestForQuotationId == id)
            .Select(o => new { o.Id, o.Code })
            .FirstOrDefaultAsync(ct);

        return new RfqDetail(
            request.Id, request.Code, request.Description, request.Status, request.ResponseDeadline,
            request.CreatedAt, request.ClosedAt,
            request.CreatedBy?.Employee?.FullName ?? request.CreatedBy?.Username ?? "—",
            request.Items.OrderBy(i => i.Id)
                .Select(i => new RfqItemDto(i.SupplyId, i.Supply.Name, i.Supply.UnitLabel, i.Quantity))
                .ToList(),
            request.Quotations.OrderBy(qt => qt.Total)
                .Select(qt => new QuotationDto(
                    qt.Id, qt.SupplierId, qt.Supplier.Name, qt.Status, qt.DeliveryDays, qt.Total, qt.Notes, qt.ReceivedAt,
                    qt.Items.OrderBy(i => i.Id).Select(i => new QuotationItemDto(i.SupplyId, i.Quantity, i.UnitPrice, i.LineTotal)).ToList()))
                .ToList(),
            request.LowestQuotation?.Id,
            order?.Id,
            order?.Code);
    }

    public async Task<RfqDetail> CreateAsync(RfqCreateRequest request, CancellationToken ct)
    {
        var supplyIds = request.Items.Select(i => i.SupplyId).Distinct().ToList();
        var activeSupplies = await db.Supplies.CountAsync(s => supplyIds.Contains(s.Id) && s.IsActive, ct);
        if (activeSupplies != supplyIds.Count)
            throw new BusinessRuleException("Uno de los insumos no existe o está inactivo.");

        var rfq = RequestForQuotation.Create(
            request.Items.Select(i => new RfqLine(i.SupplyId, i.Quantity)),
            request.ResponseDeadline,
            time.ColombiaToday(),
            currentUser.RequireUserId(),
            Normalize.OptionalText(request.Description));

        db.RequestsForQuotation.Add(rfq);
        await db.SaveChangesAsync(ct);
        return await GetAsync(rfq.Id, ct);
    }

    /// <summary>Registra la oferta de un proveedor. Las reglas del taller 2 las aplica el dominio.</summary>
    public async Task<RfqDetail> RegisterQuotationAsync(int rfqId, QuotationCreateRequest request, CancellationToken ct)
    {
        var rfq = await LoadAsync(rfqId, ct);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new BusinessRuleException("El proveedor elegido no existe.");

        var prices = request.Prices
            .GroupBy(p => p.SupplyId)
            .ToDictionary(g => g.Key, g => g.Last().UnitPrice);

        rfq.RegisterQuotation(supplier, prices, request.DeliveryDays, time.GetUtcNow().UtcDateTime,
            Normalize.OptionalText(request.Notes));

        await db.SaveChangesAsync(ct);
        return await GetAsync(rfqId, ct);
    }

    /// <summary>Elige la ganadora, cierra la solicitud y crea la orden de compra pendiente, en un solo guardado.</summary>
    public async Task<SelectQuotationResult> SelectAsync(int rfqId, int quotationId, SelectQuotationRequest request,
        CancellationToken ct)
    {
        var rfq = await LoadAsync(rfqId, ct);
        var quotation = rfq.Quotations.FirstOrDefault(q => q.Id == quotationId)
            ?? throw NotFoundException.For("la cotización", quotationId);

        var today = time.ColombiaToday();
        rfq.SelectQuotation(quotation, time.GetUtcNow().UtcDateTime);

        var order = PurchaseOrder.FromQuotation(
            quotation,
            request.ExpectedDeliveryDate ?? today.AddDays(quotation.DeliveryDays),
            today,
            currentUser.RequireUserId());

        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return new SelectQuotationResult(order.Id, order.Code);
    }

    public async Task CancelAsync(int rfqId, CancellationToken ct)
    {
        var rfq = await LoadAsync(rfqId, ct);
        rfq.Cancel(time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    private async Task<RequestForQuotation> LoadAsync(int id, CancellationToken ct) =>
        await db.RequestsForQuotation
            .Include(r => r.Items)
            .Include(r => r.Quotations).ThenInclude(q => q.Supplier)
            .Include(r => r.Quotations).ThenInclude(q => q.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw NotFoundException.For("la solicitud de cotización", id);
}
