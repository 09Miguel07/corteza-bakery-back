using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Purchasing;

/// <summary>Pantalla 05.4 · Facturas de compra: lo que nos cobran los proveedores.</summary>
public sealed class PurchaseInvoiceService(IAppDbContext db, TimeProvider time)
{
    public async Task<PagedResult<PurchaseInvoiceListItem>> ListAsync(PurchaseInvoiceQuery q, CancellationToken ct)
    {
        var today = time.ColombiaToday();
        var query = db.PurchaseInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Number))
        {
            var number = q.Number.Trim().ToUpperInvariant();
            query = query.Where(i => i.SupplierInvoiceNumber.Contains(number));
        }
        if (q.SupplierId is { } supplierId) query = query.Where(i => i.SupplierId == supplierId);
        query = q.View switch
        {
            PurchaseInvoiceView.Pending => query.Where(i => i.Status == PurchaseInvoiceStatus.Pending),
            PurchaseInvoiceView.Overdue => query.Where(i => i.Status == PurchaseInvoiceStatus.Pending && i.DueDate < today),
            PurchaseInvoiceView.Paid => query.Where(i => i.Status == PurchaseInvoiceStatus.Paid),
            _ => query,
        };

        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderBy(i => i.Status).ThenBy(i => i.DueDate).ThenBy(i => i.Id).Skip(q.Skip).Take(q.PageSize), today)
            .ToListAsync(ct);
        return new PagedResult<PurchaseInvoiceListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<PurchaseInvoiceSummary> SummaryAsync(CancellationToken ct)
    {
        var today = time.ColombiaToday();
        var weekEnd = today.AddDays(7);
        var pending = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.Status == PurchaseInvoiceStatus.Pending)
            .Select(i => new { i.Total, i.DueDate })
            .ToListAsync(ct);

        return new PurchaseInvoiceSummary(
            pending.Count,
            pending.Sum(i => i.Total),
            pending.Count(i => i.DueDate < today),
            pending.Where(i => i.DueDate < today).Sum(i => i.Total),
            pending.Where(i => i.DueDate >= today && i.DueDate <= weekEnd).Sum(i => i.Total));
    }

    public async Task<PurchaseInvoiceListItem> GetAsync(int id, CancellationToken ct) =>
        await Project(db.PurchaseInvoices.AsNoTracking().Where(i => i.Id == id), time.ColombiaToday()).FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("la factura de compra", id);

    public async Task<PurchaseInvoiceListItem> CreateAsync(PurchaseInvoiceCreateRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new BusinessRuleException("El proveedor elegido no existe.");

        var number = request.SupplierInvoiceNumber.Trim().ToUpperInvariant();
        if (await db.PurchaseInvoices.AnyAsync(i => i.SupplierId == supplier.Id && i.SupplierInvoiceNumber == number, ct))
            throw new ConflictException($"{supplier.Name} ya tiene registrada la factura {number}.");

        PurchaseOrder? order = null;
        if (request.PurchaseOrderId is { } orderId)
            order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new BusinessRuleException("La orden de compra elegida no existe.");

        var invoice = PurchaseInvoice.Create(supplier, number, request.IssueDate, request.DueDate, request.Total, order,
            Normalize.OptionalText(request.Description));

        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return await GetAsync(invoice.Id, ct);
    }

    public async Task<PurchaseInvoiceListItem> PayAsync(int id, CancellationToken ct)
    {
        var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw NotFoundException.For("la factura de compra", id);
        invoice.Pay(time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static IQueryable<PurchaseInvoiceListItem> Project(IQueryable<PurchaseInvoice> query, DateOnly today) =>
        query.Select(i => new PurchaseInvoiceListItem(
            i.Id, i.SupplierInvoiceNumber, i.SupplierId, i.Supplier.Name, i.PurchaseOrderId,
            i.PurchaseOrder != null ? i.PurchaseOrder.Code : null, i.Description, i.IssueDate, i.DueDate, i.Total, i.Status,
            i.Status == PurchaseInvoiceStatus.Pending && i.DueDate < today, i.PaidAt));
}
