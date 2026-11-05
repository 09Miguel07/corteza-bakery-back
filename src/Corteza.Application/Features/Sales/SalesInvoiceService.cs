using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Sales;

/// <summary>Pantalla 02.6 · Facturas de venta: consulta y anulación con motivo.</summary>
public sealed class SalesInvoiceService(IAppDbContext db, TimeProvider time)
{
    public async Task<PagedResult<SalesInvoiceListItem>> ListAsync(SalesInvoiceQuery q, CancellationToken ct)
    {
        var query = db.SalesInvoices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Number))
        {
            var number = q.Number.Trim().ToUpperInvariant();
            query = query.Where(i => i.Number.Contains(number));
        }
        if (q.Status is { } status) query = query.Where(i => i.Status == status);
        if (q.PaymentMethod is { } method) query = query.Where(i => i.PaymentMethod == method);
        // Las fechas del filtro son días de Colombia (UTC-5); IssuedAt está en UTC.
        if (q.From is { } from) { var f = from.ToDateTime(TimeOnly.MinValue).AddHours(5); query = query.Where(i => i.IssuedAt >= f); }
        if (q.To is { } to) { var t = to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(5); query = query.Where(i => i.IssuedAt < t); }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssuedAt).ThenByDescending(i => i.Id)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(i => new SalesInvoiceListItem(
                i.Id, i.Number, i.OrderId, i.Order.Code,
                i.Customer != null ? i.Customer.FirstNames + " " + i.Customer.LastNames : null,
                i.Total, i.PaymentMethod, i.Status, i.IssuedAt))
            .ToListAsync(ct);
        return new PagedResult<SalesInvoiceListItem>(items, total, q.Page, q.PageSize);
    }

    public Task<SalesInvoiceDetail> GetAsync(int id, CancellationToken ct) => DetailAsync(db, id, ct);

    public async Task<SalesInvoiceDetail> VoidAsync(int id, VoidSalesInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw NotFoundException.For("la factura de venta", id);
        invoice.Void(request.Reason, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(db, id, ct);
    }

    internal static async Task<SalesInvoiceDetail> DetailAsync(IAppDbContext db, int id, CancellationToken ct) =>
        await db.SalesInvoices.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new SalesInvoiceDetail(
                i.Id, i.Number, i.OrderId, i.Order.Code, i.Order.ServiceType,
                i.Order.DiningTable != null ? (short?)i.Order.DiningTable.Number : null,
                i.Customer != null ? i.Customer.FirstNames + " " + i.Customer.LastNames : null,
                i.Customer != null ? i.Customer.DocumentNumber : null,
                i.Total, i.PaymentMethod, i.AmountReceived, i.ChangeGiven, i.Status, i.IssuedBy.Username, i.IssuedAt,
                i.VoidReason, i.VoidedAt,
                i.Order.Items.OrderBy(l => l.Id)
                    .Select(l => new OrderItemDto(l.ProductId, l.Product.Name, l.Quantity, l.UnitPrice, l.LineTotal)).ToList()))
            .FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("la factura de venta", id);
}
