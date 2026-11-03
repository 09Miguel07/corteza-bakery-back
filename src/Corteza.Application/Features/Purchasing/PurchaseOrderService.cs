using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Common;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Purchasing;

/// <summary>
/// Módulo 05 · Compras. Las reglas (presupuesto, estados, recepción) viven en <see cref="PurchaseOrder"/>;
/// el servicio carga, delega y guarda una sola vez. Si una regla falla, no se guarda nada.
/// </summary>
public sealed class PurchaseOrderService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<PagedResult<PurchaseOrderListItem>> ListAsync(PurchaseOrderQuery q, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Code))
        {
            var code = q.Code.Trim().ToUpperInvariant();
            query = query.Where(o => o.Code.Contains(code));
        }

        if (q.SupplierId is { } supplierId)
            query = query.Where(o => o.SupplierId == supplierId);

        if (q.Status is { } status)
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.RequestedAt).ThenByDescending(o => o.Id)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(o => new
            {
                o.Id, o.Code, o.SupplierId, SupplierName = o.Supplier.Name, o.Status, o.ExpectedDeliveryDate, o.Total,
                ItemsCount = o.Items.Count,
                First = o.Items.OrderBy(i => i.Id).Select(i => new { i.Quantity, i.Supply.UnitLabel, i.Supply.Name }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new PurchaseOrderListItem(
                r.Id, r.Code, r.SupplierId, r.SupplierName, r.Status, r.ExpectedDeliveryDate, r.Total, r.ItemsCount,
                r.First is null ? "—"
                    : $"{Formats.Qty(r.First.Quantity)} {r.First.UnitLabel} de {r.First.Name}"
                      + (r.ItemsCount > 1 ? $" y {r.ItemsCount - 1} más" : "")))
            .ToList();

        return new PagedResult<PurchaseOrderListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<PurchaseOrderSummary> SummaryAsync(CancellationToken ct)
    {
        var today = time.ColombiaToday();
        var endOfWeek = today.AddDays((7 - (int)today.DayOfWeek) % 7); // hasta el domingo
        var active = db.PurchaseOrders.AsNoTracking();

        return new PurchaseOrderSummary(
            Pending: await active.CountAsync(o => o.Status == PurchaseOrderStatus.Pending, ct),
            Approved: await active.CountAsync(o => o.Status == PurchaseOrderStatus.Approved, ct),
            ArrivingToday: await active.CountAsync(o => o.Status == PurchaseOrderStatus.Approved && o.ExpectedDeliveryDate == today, ct),
            ArrivingThisWeek: await active.CountAsync(o =>
                (o.Status == PurchaseOrderStatus.Pending || o.Status == PurchaseOrderStatus.Approved)
                && o.ExpectedDeliveryDate >= today && o.ExpectedDeliveryDate <= endOfWeek, ct));
    }

    public async Task<PurchaseOrderDetail> GetAsync(int id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new PurchaseOrderDetail(
                o.Id, o.Code, o.Status, o.SupplierId, o.Supplier.Name, o.Supplier.TaxId, o.Supplier.Phone,
                o.Supplier.AvailableBudget, o.ExpectedDeliveryDate, o.Total, o.Notes,
                o.Quotation != null ? o.Quotation.RequestForQuotation.Code : null,
                o.RequestedAt,
                o.CreatedBy.Employee != null ? o.CreatedBy.Employee.FirstNames + " " + o.CreatedBy.Employee.LastNames : o.CreatedBy.Username,
                o.ApprovedAt,
                o.ApprovedBy == null ? null
                    : o.ApprovedBy.Employee != null ? o.ApprovedBy.Employee.FirstNames + " " + o.ApprovedBy.Employee.LastNames : o.ApprovedBy.Username,
                o.ReceivedAt, o.CancelledAt, o.CancelReason,
                o.Items.OrderBy(i => i.Id)
                    .Select(i => new PurchaseOrderItemDto(i.SupplyId, i.Supply.Name, i.Supply.UnitLabel, i.Supply.UnitSizeKg,
                        i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return order ?? throw NotFoundException.For("la orden de compra", id);
    }

    public async Task<PurchaseOrderDetail> CreateAsync(PurchaseOrderCreateRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new BusinessRuleException("El proveedor elegido no existe.");

        var supplyIds = request.Items.Select(i => i.SupplyId).Distinct().ToList();
        var activeSupplies = await db.Supplies.CountAsync(s => supplyIds.Contains(s.Id) && s.IsActive, ct);
        if (activeSupplies != supplyIds.Count)
            throw new BusinessRuleException("Uno de los insumos no existe o está inactivo.");

        var order = PurchaseOrder.Create(
            supplier,
            request.Items.Select(i => new PurchaseOrderLine(i.SupplyId, i.Quantity, i.UnitPrice)),
            request.ExpectedDeliveryDate,
            time.ColombiaToday(),
            currentUser.RequireUserId(),
            Normalize.OptionalText(request.Notes));

        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return await GetAsync(order.Id, ct);
    }

    /// <summary>Aprueba y descuenta el presupuesto del proveedor. Si no alcanza, no se guarda nada (422).</summary>
    public async Task<PurchaseOrderDetail> ApproveAsync(int id, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        var supplier = await db.Suppliers.FirstAsync(s => s.Id == order.SupplierId, ct);

        order.Approve(supplier, currentUser.RequireUserId(), time.GetUtcNow().UtcDateTime);
        supplier.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDetail> CancelAsync(int id, CancelPurchaseOrderRequest request, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.Cancel(request.Reason, time.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// "Recibir mercancía" (pantalla 05.3): cada línea entra al inventario y la orden queda recibida,
    /// todo en un solo SaveChanges (una transacción).
    /// </summary>
    public async Task<PurchaseOrderDetail> ReceiveAsync(int id, ReceivePurchaseOrderRequest request, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw NotFoundException.For("la orden de compra", id);

        var today = time.ColombiaToday();
        var receivedDate = request.ReceivedDate ?? today;
        if (receivedDate > today)
            throw new BusinessRuleException("La fecha de llegada no puede ser futura.");

        var supplyIds = order.Items.Select(i => i.SupplyId).ToList();
        var supplies = await db.Supplies.Where(s => supplyIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        order.Receive(supplies, currentUser.RequireUserId(), receivedDate, time.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<PurchaseOrder> FindAsync(int id, CancellationToken ct) =>
        await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw NotFoundException.For("la orden de compra", id);
}
