using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Sales;

/// <summary>
/// Módulo 02 · Pedidos, mesas y cobro. Las reglas (mesa ocupada, flujo de cocina, efectivo suficiente)
/// viven en <see cref="Order"/>; el servicio carga, delega y guarda una sola vez.
/// </summary>
public sealed class OrderService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<PagedResult<OrderListItem>> ListAsync(OrderQuery q, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Code))
        {
            var code = q.Code.Trim().ToUpperInvariant();
            query = query.Where(o => o.Code.Contains(code));
        }
        if (q.Status is { } status) query = query.Where(o => o.Status == status);
        if (q.ServiceType is { } type) query = query.Where(o => o.ServiceType == type);
        if (q.Open is true) query = query.Where(o => o.ClosedAt == null);
        if (q.Open is false) query = query.Where(o => o.ClosedAt != null);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(o => new
            {
                o.Id, o.Code, o.ServiceType, TableNumber = o.DiningTable != null ? (short?)o.DiningTable.Number : null,
                CustomerName = o.Customer != null ? o.Customer.FirstNames + " " + o.Customer.LastNames : null,
                o.Status, o.Total, ItemsCount = o.Items.Count, o.CreatedAt, BillRequested = o.BillRequestedAt != null,
                IsClosed = o.ClosedAt != null, InvoiceNumber = o.Invoice != null ? o.Invoice.Number : null,
                Lines = o.Items.OrderBy(i => i.Id).Select(i => new { i.Quantity, i.Product.Name }).Take(2).ToList(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new OrderListItem(
                r.Id, r.Code, r.ServiceType, r.TableNumber, r.CustomerName, r.Status, r.Total, r.ItemsCount,
                string.Join(", ", r.Lines.Select(l => $"{l.Quantity} {l.Name}")) + (r.ItemsCount > 2 ? $" y {r.ItemsCount - 2} más" : ""),
                r.CreatedAt, r.BillRequested, r.IsClosed, r.InvoiceNumber))
            .ToList();
        return new PagedResult<OrderListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<OrderSummary> SummaryAsync(CancellationToken ct)
    {
        var startOfDay = time.ColombiaStartOfDayUtc();
        var open = db.Orders.AsNoTracking().Where(o => o.ClosedAt == null);
        var paidToday = db.SalesInvoices.AsNoTracking().Where(i => i.Status == SalesInvoiceStatus.Paid && i.IssuedAt >= startOfDay);

        return new OrderSummary(
            await open.CountAsync(ct),
            await open.CountAsync(o => o.Status == OrderStatus.Preparing, ct),
            await open.CountAsync(o => o.Status == OrderStatus.Ready, ct),
            await paidToday.SumAsync(i => (decimal?)i.Total, ct) ?? 0,
            await paidToday.CountAsync(ct));
    }

    public async Task<OrderDetail> GetAsync(int id, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(o => o.Id == id)
            .Select(o => new OrderDetail(
                o.Id, o.Code, o.ServiceType, o.DiningTableId, o.DiningTable != null ? (short?)o.DiningTable.Number : null,
                o.CustomerId, o.Customer != null ? o.Customer.FirstNames + " " + o.Customer.LastNames : null,
                o.Status, o.Total, o.ServedBy.Username, o.CreatedAt, o.PreparingAt, o.ReadyAt, o.DeliveredAt,
                o.BillRequestedAt, o.ClosedAt, o.Invoice != null ? o.Invoice.Id : null, o.Invoice != null ? o.Invoice.Number : null,
                o.Items.OrderBy(i => i.Id)
                    .Select(i => new OrderItemDto(i.ProductId, i.Product.Name, i.Quantity, i.UnitPrice, i.LineTotal)).ToList()))
            .FirstOrDefaultAsync(ct)
        ?? throw NotFoundException.For("el pedido", id);

    public async Task<OrderDetail> CreateAsync(OrderCreateRequest request, CancellationToken ct)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != productIds.Count) throw new BusinessRuleException("Uno de los productos no existe.");

        DiningTable? table = null;
        var busy = false;
        if (request.DiningTableId is { } tableId)
        {
            table = await db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId, ct)
                ?? throw new BusinessRuleException("La mesa elegida no existe.");
            busy = await db.Orders.AnyAsync(o => o.DiningTableId == tableId && o.ClosedAt == null, ct);
        }

        Customer? customer = null;
        if (request.CustomerId is { } customerId)
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new BusinessRuleException("El cliente elegido no existe.");

        var order = Order.Create(request.ServiceType, table, busy, customer,
            request.Items.Select(i => new OrderLine(products[i.ProductId], i.Quantity)).ToList(),
            currentUser.RequireUserId(), Now);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return await GetAsync(order.Id, ct);
    }

    public async Task<OrderDetail> AdvanceAsync(int id, AdvanceOrderRequest request, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        if (request.Status == OrderStatus.Cancelled) order.Cancel(Now);
        else order.AdvanceTo(request.Status, Now);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDetail> RequestBillAsync(int id, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.RequestBill(Now);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Pantalla 02.5 · Cobrar: emite la factura de venta, calcula el cambio y libera la mesa.</summary>
    public async Task<SalesInvoiceDetail> CheckoutAsync(int id, CheckoutRequest request, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Invoice).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw NotFoundException.For("el pedido", id);

        var invoice = order.Checkout(request.PaymentMethod, request.AmountReceived, currentUser.RequireUserId(), Now);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return await SalesInvoiceService.DetailAsync(db, invoice.Id, ct);
    }

    /// <summary>Pantalla 02.4 · Mesas: libre, ocupada o por cobrar, derivado del pedido abierto.</summary>
    public async Task<IReadOnlyList<DiningTableStatus>> TablesAsync(CancellationToken ct)
    {
        var tables = await db.DiningTables.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Number).ToListAsync(ct);
        var open = await db.Orders.AsNoTracking()
            .Where(o => o.DiningTableId != null && o.ClosedAt == null)
            .ToListAsync(ct);

        return tables.Select(t =>
        {
            var order = open.FirstOrDefault(o => o.DiningTableId == t.Id);
            return new DiningTableStatus(t.Id, t.Number, t.Capacity, Order.StateOf(order), order?.Id, order?.Code, order?.Total, order?.CreatedAt);
        }).ToList();
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private async Task<Order> FindAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Invoice).FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw NotFoundException.For("el pedido", id);
}
