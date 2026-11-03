using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Time;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Inventory;

public sealed class SupplyService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<PagedResult<SupplyListItem>> ListAsync(SupplyQuery q, CancellationToken ct)
    {
        var query = db.Supplies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = Normalize.Text(q.Search);
            query = query.Where(s => s.Name.Contains(search) || s.Category.Name.Contains(search));
        }

        if (q.CategoryId is { } categoryId)
            query = query.Where(s => s.CategoryId == categoryId);

        if (q.Active is not null)
            query = query.Where(s => s.IsActive == q.Active);

        query = q.Stock switch
        {
            StockStatus.Out => query.Where(s => s.CurrentStock <= 0),
            StockStatus.Low => query.Where(s => s.CurrentStock > 0 && s.CurrentStock < s.MinStock),
            StockStatus.Ok => query.Where(s => s.CurrentStock > 0 && s.CurrentStock >= s.MinStock),
            _ => query,
        };

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(s => s.Name)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return new PagedResult<SupplyListItem>(rows.Select(ToItem).ToList(), total, q.Page, q.PageSize);
    }

    /// <summary>Insumos agotados o por debajo del mínimo (alertas de 03.0 y 07.1).</summary>
    public async Task<IReadOnlyList<SupplyListItem>> LowStockAsync(CancellationToken ct)
    {
        var rows = await db.Supplies.AsNoTracking()
            .Where(s => s.IsActive && s.CurrentStock < s.MinStock)
            .OrderBy(s => s.CurrentStock / (s.MinStock == 0 ? 1 : s.MinStock))
            .Select(Projection)
            .ToListAsync(ct);

        return rows.Select(ToItem).ToList();
    }

    public async Task<SupplyListItem> GetAsync(int id, CancellationToken ct)
    {
        var row = await db.Supplies.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("el insumo", id);

        return ToItem(row);
    }

    public async Task<SupplyListItem> CreateAsync(SupplyCreateRequest request, CancellationToken ct)
    {
        var name = Normalize.Text(request.Name);
        await EnsureUniqueNameAsync(name, excludeId: null, ct);
        await ValidateReferencesAsync(request, ct);

        var supply = Supply.Create(
            name, request.CategoryId, Normalize.Text(request.PurchaseUnit), Normalize.Text(request.UnitLabel),
            request.UnitSizeKg, request.InitialStock, request.MinStock, request.MainSupplierId,
            currentUser.RequireUserId(), time.ColombiaToday());

        db.Supplies.Add(supply);
        await db.SaveChangesAsync(ct);
        return await GetAsync(supply.Id, ct);
    }

    /// <summary>Actualiza los datos del insumo. La existencia solo cambia con movimientos.</summary>
    public async Task<SupplyListItem> UpdateAsync(int id, SupplyUpdateRequest request, CancellationToken ct)
    {
        var supply = await db.Supplies.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el insumo", id);

        var name = Normalize.Text(request.Name);
        await EnsureUniqueNameAsync(name, excludeId: id, ct);
        await ValidateReferencesAsync(request, ct);

        supply.UpdateDetails(
            name, request.CategoryId, Normalize.Text(request.PurchaseUnit), Normalize.Text(request.UnitLabel),
            request.UnitSizeKg, request.MinStock, request.MainSupplierId);
        supply.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var supply = await db.Supplies.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el insumo", id);
        if (supply.IsActive == active) return;

        supply.SetActive(active);
        supply.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken ct)
    {
        if (await db.Supplies.AnyAsync(s => s.Name == name && s.Id != excludeId, ct))
            throw new ConflictException($"Ya existe un insumo llamado \"{name}\".");
    }

    private async Task ValidateReferencesAsync(SupplyUpdateRequest request, CancellationToken ct)
    {
        if (!await db.SupplyCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new BusinessRuleException("La categoría elegida no existe.");

        if (request.MainSupplierId is { } supplierId
            && !await db.Suppliers.AnyAsync(s => s.Id == supplierId && s.IsActive, ct))
            throw new BusinessRuleException("El proveedor principal no existe o está inactivo.");
    }


    // Fila plana que EF traduce a SQL; el estado se calcula en memoria.
    private sealed record SupplyRow(
        int Id, string Code, string Name, int CategoryId, string CategoryName, string PurchaseUnit, string UnitLabel,
        decimal? UnitSizeKg, decimal CurrentStock, decimal MinStock, int? MainSupplierId, string? MainSupplierName, bool IsActive);

    private static readonly System.Linq.Expressions.Expression<Func<Supply, SupplyRow>> Projection = s => new SupplyRow(
        s.Id, s.Code, s.Name, s.CategoryId, s.Category.Name, s.PurchaseUnit, s.UnitLabel, s.UnitSizeKg,
        s.CurrentStock, s.MinStock, s.MainSupplierId, s.MainSupplier != null ? s.MainSupplier.Name : null, s.IsActive);

    private static SupplyListItem ToItem(SupplyRow r) => new(
        r.Id, r.Code, r.Name, r.CategoryId, r.CategoryName, r.PurchaseUnit, r.UnitLabel, r.UnitSizeKg,
        r.CurrentStock, r.MinStock, Supply.StatusOf(r.CurrentStock, r.MinStock),
        r.MainSupplierId, r.MainSupplierName, r.IsActive);

}
